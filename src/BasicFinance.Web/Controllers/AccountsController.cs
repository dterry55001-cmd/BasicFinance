using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class AccountsController(ApplicationDbContext database, AccountBalanceService balances) : Controller
{
    public async Task<IActionResult> Index(bool includeArchived = false)
    {
        var query = database.Accounts.AsNoTracking();
        if (!includeArchived)
            query = query.Where(account => account.Status != AccountStatus.Archived);

        ViewData["IncludeArchived"] = includeArchived;
        var accounts = await query.OrderBy(account => account.Name).ToListAsync();
        ViewData["Balances"] = await Task.WhenAll(accounts.Select(async account =>
            new KeyValuePair<int, decimal>(account.Id, await balances.GetBalanceAsync(account.Id))));
        return View(accounts);
    }

    public async Task<IActionResult> Details(int id)
    {
        var account = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (account is null) return NotFound();

        ViewBag.CurrentBalance = await balances.GetBalanceAsync(id);
        ViewBag.RecentTransactions = await database.Transactions.AsNoTracking()
            .Where(t => t.AccountId == id || t.RelatedAccountId == id)
            .Include(t => t.Account).Include(t => t.RelatedAccount)
            .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Take(10).ToListAsync();
        return View(account);
    }

    public IActionResult Create()
    {
        PopulateAccountSubtypes();
        return View(new AccountInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccountInputModel input)
    {
        ValidateAccountSubtypes(input);
        if (!ModelState.IsValid)
        {
            PopulateAccountSubtypes();
            return View(input);
        }
        var account = new Account
        {
            Name = input.Name.Trim(), Type = input.Type, RetirementType = input.Type == AccountType.Retirement ? input.RetirementType : null,
            DebtType = input.Type == AccountType.Debt ? input.DebtType : null,
            Institution = CleanOptional(input.Institution),
            InterestRatePercent = input.Type is AccountType.Debt or AccountType.Checking or AccountType.Savings ? input.InterestRatePercent : null,
            CreditLimit = input.Type == AccountType.Debt ? input.CreditLimit : null,
            MinimumPayment = input.Type == AccountType.Debt ? input.MinimumPayment : null,
            PaymentDueDay = input.Type == AccountType.Debt ? input.PaymentDueDay : null,
            OriginalLoanAmount = input.Type == AccountType.Debt ? input.OriginalLoanAmount : null,
            LoanTermMonths = input.Type == AccountType.Debt ? input.LoanTermMonths : null,
            MaturityDate = input.Type == AccountType.Debt ? input.MaturityDate : null,
            EmployerName = input.Type == AccountType.Retirement ? CleanOptional(input.EmployerName) : null,
            EmployerMatchPercent = input.Type == AccountType.Retirement ? input.EmployerMatchPercent : null,
            TaxTreatment = input.Type == AccountType.Retirement ? CleanOptional(input.TaxTreatment) : null,
            BrokerageRegistrationType = input.Type == AccountType.Brokerage ? CleanOptional(input.BrokerageRegistrationType) : null,
            Notes = CleanOptional(input.Notes),
            CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant(),
            OpeningBalance = input.OpeningBalance, OpenedOn = input.OpenedOn,
            Status = input.Status, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow
        };
        database.Accounts.Add(account);
        await database.SaveChangesAsync();
        TempData["SuccessMessage"] = "Account created.";
        return RedirectToAction(nameof(Details), new { id = account.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var account = await database.Accounts.FindAsync(id);
        if (account is null) return NotFound();
        PopulateAccountSubtypes();
        return View(new AccountInputModel
        {
            Name = account.Name, Type = account.Type, RetirementType = account.RetirementType, DebtType = account.DebtType, Institution = account.Institution,
            CurrencyCode = account.CurrencyCode,
            InterestRatePercent = account.InterestRatePercent,
            CreditLimit = account.CreditLimit,
            MinimumPayment = account.MinimumPayment,
            PaymentDueDay = account.PaymentDueDay,
            OriginalLoanAmount = account.OriginalLoanAmount,
            LoanTermMonths = account.LoanTermMonths,
            MaturityDate = account.MaturityDate,
            EmployerName = account.EmployerName,
            EmployerMatchPercent = account.EmployerMatchPercent,
            TaxTreatment = account.TaxTreatment,
            BrokerageRegistrationType = account.BrokerageRegistrationType,
            Notes = account.Notes,
            OpeningBalance = account.OpeningBalance, OpenedOn = account.OpenedOn, Status = account.Status
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AccountInputModel input)
    {
        ValidateAccountSubtypes(input);
        if (!ModelState.IsValid)
        {
            PopulateAccountSubtypes();
            return View(input);
        }
        var account = await database.Accounts.FindAsync(id);
        if (account is null) return NotFound();
        account.Name = input.Name.Trim(); account.Type = input.Type;
        account.RetirementType = input.Type == AccountType.Retirement ? input.RetirementType : null;
        account.DebtType = input.Type == AccountType.Debt ? input.DebtType : null;
        account.Institution = CleanOptional(input.Institution);
        account.InterestRatePercent = input.Type is AccountType.Debt or AccountType.Checking or AccountType.Savings ? input.InterestRatePercent : null;
        account.CreditLimit = input.Type == AccountType.Debt ? input.CreditLimit : null;
        account.MinimumPayment = input.Type == AccountType.Debt ? input.MinimumPayment : null;
        account.PaymentDueDay = input.Type == AccountType.Debt ? input.PaymentDueDay : null;
        account.OriginalLoanAmount = input.Type == AccountType.Debt ? input.OriginalLoanAmount : null;
        account.LoanTermMonths = input.Type == AccountType.Debt ? input.LoanTermMonths : null;
        account.MaturityDate = input.Type == AccountType.Debt ? input.MaturityDate : null;
        account.EmployerName = input.Type == AccountType.Retirement ? CleanOptional(input.EmployerName) : null;
        account.EmployerMatchPercent = input.Type == AccountType.Retirement ? input.EmployerMatchPercent : null;
        account.TaxTreatment = input.Type == AccountType.Retirement ? CleanOptional(input.TaxTreatment) : null;
        account.BrokerageRegistrationType = input.Type == AccountType.Brokerage ? CleanOptional(input.BrokerageRegistrationType) : null;
        account.Notes = CleanOptional(input.Notes);
        account.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        account.OpeningBalance = input.OpeningBalance; account.OpenedOn = input.OpenedOn;
        account.Status = input.Status; account.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();
        TempData["SuccessMessage"] = "Account updated.";
        return RedirectToAction(nameof(Details), new { id = account.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var account = await database.Accounts.FindAsync(id);
        if (account is null) return NotFound();
        account.Status = AccountStatus.Archived;
        account.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();
        TempData["SuccessMessage"] = "Account archived. Its history remains in the database.";
        return RedirectToAction(nameof(Index), new { includeArchived = true });
    }

    private void PopulateAccountSubtypes()
    {
        ViewBag.RetirementTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            Enum.GetValues<RetirementAccountType>().Select(type => new { Value = type, Text = RetirementTypeLabel(type) }),
            "Value", "Text");
        ViewBag.DebtTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            Enum.GetValues<DebtAccountType>().Select(type => new { Value = type, Text = DebtTypeLabel(type) }),
            "Value", "Text");
    }

    private void ValidateAccountSubtypes(AccountInputModel input)
    {
        if (input.Type == AccountType.Retirement && !input.RetirementType.HasValue)
            ModelState.AddModelError(nameof(input.RetirementType), "Choose a retirement account type.");
        else if (input.Type == AccountType.Retirement && !Enum.IsDefined(input.RetirementType!.Value))
            ModelState.AddModelError(nameof(input.RetirementType), "Choose a valid retirement account type.");
        if (input.Type == AccountType.Debt && !input.DebtType.HasValue)
            ModelState.AddModelError(nameof(input.DebtType), "Choose a debt account type.");
        else if (input.Type == AccountType.Debt && !Enum.IsDefined(input.DebtType!.Value))
            ModelState.AddModelError(nameof(input.DebtType), "Choose a valid debt account type.");
        if (input.Type != AccountType.Retirement) input.RetirementType = null;
        if (input.Type != AccountType.Debt) input.DebtType = null;
    }

    private static string DebtTypeLabel(DebtAccountType type) => type switch
    {
        DebtAccountType.CreditCard => "Credit card",
        DebtAccountType.Mortgage => "Mortgage",
        DebtAccountType.HomeEquityLoan => "Home equity loan",
        DebtAccountType.HomeEquityLineOfCredit => "Home equity line of credit (HELOC)",
        DebtAccountType.AutoLoan => "Auto loan",
        DebtAccountType.StudentLoan => "Student loan",
        DebtAccountType.PersonalLoan => "Personal loan",
        DebtAccountType.BusinessLoan => "Business loan",
        DebtAccountType.MedicalDebt => "Medical debt",
        DebtAccountType.BuyNowPayLater => "Buy now, pay later",
        DebtAccountType.LineOfCredit => "Other line of credit",
        DebtAccountType.TaxDebt => "Tax debt",
        _ => "Other debt"
    };

    private static string RetirementTypeLabel(RetirementAccountType type) => type switch
    {
        RetirementAccountType.Employer401k => "401(k) — Employer-sponsored",
        RetirementAccountType.Employer401a => "401(a) — Employer-sponsored",
        RetirementAccountType.Employer403b => "403(b) — Tax-sheltered annuity",
        RetirementAccountType.Government457b => "457(b) — Government deferred compensation",
        RetirementAccountType.ThriftSavingsPlan => "Thrift Savings Plan (TSP)",
        RetirementAccountType.TraditionalIra => "Traditional IRA",
        RetirementAccountType.RothIra => "Roth IRA",
        RetirementAccountType.RolloverIra => "Rollover IRA",
        RetirementAccountType.InheritedIra => "Inherited IRA",
        RetirementAccountType.SepIra => "SEP IRA",
        RetirementAccountType.SimpleIra => "SIMPLE IRA",
        RetirementAccountType.Solo401k => "Solo / Individual 401(k)",
        RetirementAccountType.DefinedBenefitPension => "Defined-benefit pension",
        RetirementAccountType.CashBalancePension => "Cash-balance pension",
        RetirementAccountType.Annuity => "Retirement annuity",
        _ => "Other retirement account"
    };

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
