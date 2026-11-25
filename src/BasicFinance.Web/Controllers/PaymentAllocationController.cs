using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class PaymentAllocationController(
    ApplicationDbContext database,
    AccountBalanceService balances,
    DebtManagementService debtService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new PaymentAllocationInputModel();
        await PopulateCurrenciesAsync(model.CurrencyCode, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(PaymentAllocationInputModel model, CancellationToken cancellationToken)
    {
        await PopulateCurrenciesAsync(model.CurrencyCode, cancellationToken);
        if (!ModelState.IsValid) return View(model);

        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && a.CurrencyCode == model.CurrencyCode &&
                (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);

        var debts = new List<DebtPayoffDebtInput>();
        foreach (var account in accounts)
        {
            var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            if (balance <= 0m) continue;
            if (!account.InterestRatePercent.HasValue || !account.MinimumPayment.HasValue ||
                account.MinimumPayment.Value <= 0m)
            {
                model.MissingInputs.Add(account.Name +
                    (!account.InterestRatePercent.HasValue ? " (APR missing)" : string.Empty) +
                    (!account.MinimumPayment.HasValue || account.MinimumPayment.Value <= 0m ? " (positive minimum payment missing)" : string.Empty));
                continue;
            }
            debts.Add(new DebtPayoffDebtInput
            {
                AccountId = account.Id, Name = account.Name, Balance = balance,
                AnnualRatePercent = account.InterestRatePercent.Value,
                MinimumPayment = account.MinimumPayment.Value
            });
        }

        model.Debts = debts
            .OrderByDescending(d => d.AnnualRatePercent).ThenByDescending(d => d.Balance)
            .Select((d, index) => new PaymentAllocationDebtRow
            {
                AccountId = d.AccountId, AccountName = d.Name, Balance = d.Balance,
                AnnualRatePercent = d.AnnualRatePercent, MinimumPayment = d.MinimumPayment,
                AvalanchePriority = index + 1
            }).ToList();
        var snowballOrder = debts.OrderBy(d => d.Balance).ThenByDescending(d => d.AnnualRatePercent)
            .Select((d, index) => new { d.AccountId, Priority = index + 1 })
            .ToDictionary(x => x.AccountId, x => x.Priority);
        foreach (var row in model.Debts) row.SnowballPriority = snowballOrder[row.AccountId];

        if (model.MissingInputs.Count > 0)
        {
            model.Notice = "Complete missing APR and minimum-payment details before comparing allocations. Incomplete accounts are not assigned estimated values.";
            return View(model);
        }
        if (debts.Count == 0)
        {
            model.Notice = "No active debt balances were found for this currency.";
            return View(model);
        }

        try
        {
            model.CurrentPlan = debtService.CalculatePayoff(debts, 0m, avalanche: true,
                startDate: DateTime.Today, applyMinimumPaymentFloor: false);
            model.SnowballPlan = debtService.CalculatePayoff(debts, model.ExtraMonthlyPayment,
                avalanche: false, startDate: DateTime.Today, applyMinimumPaymentFloor: false);
            model.AvalanchePlan = debtService.CalculatePayoff(debts, model.ExtraMonthlyPayment,
                avalanche: true, startDate: DateTime.Today, applyMinimumPaymentFloor: false);

            if (model.CurrentPlan.ReachedDebtFree && model.AvalanchePlan.ReachedDebtFree)
            {
                model.InterestSavingsVsCurrent = Math.Max(0m,
                    model.CurrentPlan.TotalInterest - model.AvalanchePlan.TotalInterest);
                model.MonthsSavedVsCurrent = Math.Max(0,
                    model.CurrentPlan.MonthsToPayoff - model.AvalanchePlan.MonthsToPayoff);
            }
            foreach (var row in model.Debts)
            {
                row.AvalanchePayoffDate = model.AvalanchePlan.Debts.First(x => x.AccountId == row.AccountId).PayoffDate;
                row.SnowballPayoffDate = model.SnowballPlan.Debts.First(x => x.AccountId == row.AccountId).PayoffDate;
            }
            model.Notice = "Avalanche directs extra money to the highest APR debt after minimums. Snowball directs it to the smallest balance. These are simulations only; no account or payment records have been changed.";
        }
        catch (InvalidOperationException ex) { model.Notice = ex.Message; }
        return View(model);
    }

    private async Task PopulateCurrenciesAsync(string selected, CancellationToken cancellationToken)
    {
        var currencies = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active &&
                (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .Select(a => a.CurrencyCode).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        if (!currencies.Contains("USD")) currencies.Insert(0, "USD");
        ViewBag.DebtCurrencies = new SelectList(currencies, selected);
    }
}
