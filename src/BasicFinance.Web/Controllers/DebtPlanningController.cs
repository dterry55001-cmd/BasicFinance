using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtPlanningController(ApplicationDbContext database, AccountBalanceService balances, DebtManagementService debtService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> DebtToIncome(string currencyCode = "USD", CancellationToken cancellationToken = default)
    {
        currencyCode = currencyCode.ToUpperInvariant();
        if (currencyCode.Length != 3) currencyCode = "USD";
        ViewBag.CurrencyCode = currencyCode;
        ViewBag.History = await database.DebtToIncomeRecords.AsNoTracking()
            .Where(r => r.CurrencyCode == currencyCode).OrderByDescending(r => r.Month).Take(36).ToListAsync(cancellationToken);
        return View(new DebtToIncomeRecord { Month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), CurrencyCode = currencyCode });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DebtToIncome(DebtToIncomeRecord input, CancellationToken cancellationToken)
    {
        input.Month = new DateTime(input.Month.Year, input.Month.Month, 1);
        input.CurrencyCode = (input.CurrencyCode ?? "USD").ToUpperInvariant();
        var existing = await database.DebtToIncomeRecords.FirstOrDefaultAsync(r => r.Month == input.Month && r.CurrencyCode == input.CurrencyCode, cancellationToken);
        if (existing is not null) ModelState.AddModelError(string.Empty, "A record already exists for this month and currency. Edit the existing record instead.");
        if (!ModelState.IsValid)
        {
            ViewBag.CurrencyCode = input.CurrencyCode;
            ViewBag.History = await database.DebtToIncomeRecords.AsNoTracking().Where(r => r.CurrencyCode == input.CurrencyCode)
                .OrderByDescending(r => r.Month).Take(36).ToListAsync(cancellationToken);
            return View(input);
        }
        input.CreatedUtc = input.UpdatedUtc = DateTime.UtcNow;
        database.DebtToIncomeRecords.Add(input);
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Monthly income and debt obligations saved.";
        return RedirectToAction(nameof(DebtToIncome), new { currencyCode = input.CurrencyCode });
    }

    [HttpGet]
    public async Task<IActionResult> EarlyPayoff(int accountId, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId &&
            a.Status == AccountStatus.Active && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard), cancellationToken);
        if (account is null) return NotFound();
        ViewBag.Account = account;
        ViewBag.Balance = Math.Max(0m, await balances.GetBalanceAsync(accountId, cancellationToken));
        return View(new EarlyPayoffScenarioInput { AccountId = accountId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EarlyPayoff(EarlyPayoffScenarioInput input, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == input.AccountId &&
            a.Status == AccountStatus.Active && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard), cancellationToken);
        if (account is null) return NotFound();
        var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
        ViewBag.Account = account;
        ViewBag.Balance = balance;
        if (balance <= 0) ModelState.AddModelError(string.Empty, "This account has no outstanding balance.");
        if (ModelState.IsValid)
        {
            try
            {
                ViewBag.Scenarios = debtService.CalculateEarlyPayoffScenarios(balance, account.InterestRatePercent ?? 0m,
                    account.MinimumPayment ?? 0m, input.OneTimePayment, input.OneTimePaymentMonth, input.ExtraMonthlyPayment);
            }
            catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }
        return View(input);
    }
}
