using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class AccountManagementController(
    ApplicationDbContext database,
    AccountBalanceService balances,
    DebtManagementService debtService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active)
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var rows = new List<AccountManagementRow>();
        foreach (var account in accounts)
        {
            var balance = await balances.GetBalanceAsync(account.Id, cancellationToken);
            rows.Add(new AccountManagementRow
            {
                Account = account,
                Balance = balance,
                UtilizationPercent = account.CreditLimit is > 0
                    ? Math.Round(Math.Max(0, balance) / account.CreditLimit.Value * 100m, 1)
                    : null
            });
        }
        ViewBag.Rows = rows;
        ViewBag.DebtTotals = rows.Where(r => r.Account.Type is AccountType.Debt or AccountType.CreditCard)
            .GroupBy(r => r.Account.CurrencyCode)
            .Select(group => new DebtCurrencyTotal { CurrencyCode = group.Key, Amount = group.Sum(r => Math.Max(0, r.Balance)) })
            .OrderBy(item => item.CurrencyCode).ToList();
        ViewBag.InterestEarnedTotals = await database.InterestRecords.AsNoTracking().Where(r => r.Type == InterestRecordType.Earned)
            .GroupBy(r => r.Account.CurrencyCode)
            .Select(group => new InterestCurrencyTotal { CurrencyCode = group.Key, Amount = group.Sum(r => r.Amount) })
            .OrderBy(item => item.CurrencyCode).ToListAsync(cancellationToken);
        ViewBag.InterestChargedTotals = await database.InterestRecords.AsNoTracking().Where(r => r.Type == InterestRecordType.Charged)
            .GroupBy(r => r.Account.CurrencyCode)
            .Select(group => new InterestCurrencyTotal { CurrencyCode = group.Key, Amount = group.Sum(r => r.Amount) })
            .OrderBy(item => item.CurrencyCode).ToListAsync(cancellationToken);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Payoff(CancellationToken cancellationToken)
    {
        await PopulateDebtCurrenciesAsync("USD", cancellationToken);
        return View(new DebtPayoffInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Payoff(DebtPayoffInputModel input, CancellationToken cancellationToken)
    {
        await PopulateDebtCurrenciesAsync(input.CurrencyCode, cancellationToken);
        if (!ModelState.IsValid) return View(input);
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && a.CurrencyCode == input.CurrencyCode && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var debts = new List<DebtPayoffDebtInput>();
        foreach (var account in accounts)
        {
            var balance = await balances.GetBalanceAsync(account.Id, cancellationToken);
            if (balance <= 0) continue;
            debts.Add(new DebtPayoffDebtInput
            {
                AccountId = account.Id,
                Name = account.Name,
                Balance = balance,
                AnnualRatePercent = account.InterestRatePercent ?? 0,
                MinimumPayment = account.MinimumPayment ?? 0
            });
        }
        ViewBag.Snowball = debtService.CalculatePayoff(debts, input.ExtraMonthlyPayment, false);
        ViewBag.Avalanche = debtService.CalculatePayoff(debts, input.ExtraMonthlyPayment, true);
        ViewBag.EstimatedMinimums = debts.Any(d => d.MinimumPayment <= 0);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Amortization(int? accountId, CancellationToken cancellationToken)
    {
        await PopulateDebtAccountsAsync(cancellationToken);
        var account = accountId.HasValue
            ? await database.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId && a.Status == AccountStatus.Active, cancellationToken)
            : null;
        var input = new LoanAmortizationInput();
        if (account is not null)
        {
            input.AccountId = account.Id;
            input.Principal = Math.Max(0, await balances.GetBalanceAsync(account.Id, cancellationToken));
            input.AnnualRatePercent = account.InterestRatePercent ?? 0;
            input.TermMonths = account.LoanTermMonths ?? 60;
        }
        return View(input);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Amortization(LoanAmortizationInput input, CancellationToken cancellationToken)
    {
        await PopulateDebtAccountsAsync(cancellationToken);
        if (!ModelState.IsValid) return View(input);
        try { ViewBag.Result = debtService.CalculateAmortization(input); }
        catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> BalanceHistory(int accountId, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null) return NotFound();
        ViewBag.Account = account;
        ViewBag.CurrentBalance = await balances.GetBalanceAsync(accountId, cancellationToken);
        ViewBag.Snapshots = await database.AccountBalanceSnapshots.AsNoTracking()
            .Where(s => s.AccountId == accountId).OrderByDescending(s => s.SnapshotDate).ToListAsync(cancellationToken);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureBalance(int accountId, DateTime snapshotDate, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null) return NotFound();
        var date = snapshotDate == default ? DateTime.Today : snapshotDate.Date;
        var exists = await database.AccountBalanceSnapshots.AnyAsync(s => s.AccountId == accountId && s.SnapshotDate == date, cancellationToken);
        if (exists)
        {
            TempData["ErrorMessage"] = "A balance snapshot already exists for this account and date.";
            return RedirectToAction(nameof(BalanceHistory), new { accountId });
        }
        database.AccountBalanceSnapshots.Add(new AccountBalanceSnapshot
        {
            AccountId = accountId, SnapshotDate = date,
            Balance = await balances.GetBalanceAtAsync(accountId, date, cancellationToken)
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Historical balance snapshot saved.";
        return RedirectToAction(nameof(BalanceHistory), new { accountId });
    }

    [HttpGet]
    public async Task<IActionResult> Interest(int? accountId, CancellationToken cancellationToken)
    {
        await PopulateAllAccountsAsync(accountId, cancellationToken);
        var query = database.InterestRecords.AsNoTracking().Include(r => r.Account).AsQueryable();
        if (accountId.HasValue) query = query.Where(r => r.AccountId == accountId);
        return View(await query.OrderByDescending(r => r.InterestDate).ThenByDescending(r => r.Id).Take(500).ToListAsync(cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInterest([Bind("AccountId,InterestDate,Type,Amount,Description,Notes")] InterestRecord input, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.FirstOrDefaultAsync(a => a.Id == input.AccountId && a.Status != AccountStatus.Archived, cancellationToken);
        if (account is null) ModelState.AddModelError(nameof(input.AccountId), "Choose an active account.");
        if (input.Amount <= 0) ModelState.AddModelError(nameof(input.Amount), "Enter an amount greater than zero.");
        if (!ModelState.IsValid)
        {
            await PopulateAllAccountsAsync(input.AccountId, cancellationToken);
            return View("Interest", await database.InterestRecords.AsNoTracking().Include(r => r.Account)
                .OrderByDescending(r => r.InterestDate).Take(500).ToListAsync(cancellationToken));
        }
        input.Description = string.IsNullOrWhiteSpace(input.Description) ? "Interest" : input.Description.Trim();
        input.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        input.InterestDate = input.InterestDate.Date;
        input.CreatedUtc = DateTime.UtcNow;
        database.InterestRecords.Add(input);
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Interest record saved. It is tracked separately and does not alter account balance.";
        return RedirectToAction(nameof(Interest), new { accountId = input.AccountId });
    }

    private async Task PopulateDebtCurrenciesAsync(string selectedCurrency, CancellationToken cancellationToken)
    {
        var currencies = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .Select(a => a.CurrencyCode).Distinct().OrderBy(code => code).ToListAsync(cancellationToken);
        if (currencies.Count == 0 && !currencies.Contains("USD")) currencies.Add("USD");
        ViewBag.DebtCurrencies = new SelectList(currencies, selectedCurrency);
    }

    private async Task PopulateDebtAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        ViewBag.DebtAccounts = new SelectList(accounts, "Id", "Name");
    }

    private async Task PopulateAllAccountsAsync(int? selected, CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status != AccountStatus.Archived).OrderBy(a => a.Name).ToListAsync(cancellationToken);
        ViewBag.Accounts = new SelectList(accounts, "Id", "Name", selected);
    }
}

public sealed class AccountManagementRow
{
    public Account Account { get; set; } = null!;
    public decimal Balance { get; set; }
    public decimal? UtilizationPercent { get; set; }
}

public sealed class DebtCurrencyTotal
{
    public string CurrencyCode { get; set; } = "USD";
    public decimal Amount { get; set; }
}

public sealed class InterestCurrencyTotal
{
    public string CurrencyCode { get; set; } = "USD";
    public decimal Amount { get; set; }
}
