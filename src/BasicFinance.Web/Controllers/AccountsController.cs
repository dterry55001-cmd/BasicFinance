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
        PopulateRetirementTypes();
        return View(new AccountInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccountInputModel input)
    {
        ValidateRetirementType(input);
        if (!ModelState.IsValid)
        {
            PopulateRetirementTypes();
            return View(input);
        }
        var account = new Account
        {
            Name = input.Name.Trim(), Type = input.Type, RetirementType = input.Type == AccountType.Retirement ? input.RetirementType : null,
            Institution = CleanOptional(input.Institution),
            AccountNumberLast4 = CleanOptional(input.AccountNumberLast4),
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
        PopulateRetirementTypes();
        return View(new AccountInputModel
        {
            Name = account.Name, Type = account.Type, RetirementType = account.RetirementType, Institution = account.Institution,
            AccountNumberLast4 = account.AccountNumberLast4, CurrencyCode = account.CurrencyCode,
            OpeningBalance = account.OpeningBalance, OpenedOn = account.OpenedOn, Status = account.Status
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AccountInputModel input)
    {
        ValidateRetirementType(input);
        if (!ModelState.IsValid)
        {
            PopulateRetirementTypes();
            return View(input);
        }
        var account = await database.Accounts.FindAsync(id);
        if (account is null) return NotFound();
        account.Name = input.Name.Trim(); account.Type = input.Type;
        account.RetirementType = input.Type == AccountType.Retirement ? input.RetirementType : null;
        account.Institution = CleanOptional(input.Institution);
        account.AccountNumberLast4 = CleanOptional(input.AccountNumberLast4);
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

    private void PopulateRetirementTypes()
    {
        ViewBag.RetirementTypes = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
            Enum.GetValues<RetirementAccountType>()
                .Select(type => new { Value = type, Text = RetirementTypeLabel(type) }),
            "Value", "Text");
    }

    private void ValidateRetirementType(AccountInputModel input)
    {
        if (input.Type == AccountType.Retirement && !input.RetirementType.HasValue)
            ModelState.AddModelError(nameof(input.RetirementType), "Choose a retirement account type.");
        else if (input.Type == AccountType.Retirement &&
                 !Enum.IsDefined(input.RetirementType!.Value))
            ModelState.AddModelError(nameof(input.RetirementType), "Choose a valid retirement account type.");
        if (input.Type != AccountType.Retirement)
            input.RetirementType = null;
    }

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
