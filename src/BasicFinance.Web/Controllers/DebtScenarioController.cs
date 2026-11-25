using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtScenarioController(ApplicationDbContext database, AccountBalanceService balances) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await LoadDebtsAsync(cancellationToken);
        return View(new DebtScenarioInput());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DebtScenarioInput input, CancellationToken cancellationToken)
    {
        var debts = await LoadDebtsAsync(cancellationToken);
        if (debts.Count == 0)
            ModelState.AddModelError(string.Empty, "No active debts with outstanding balances were found.");
        if (input.LumpSumPayment > 0 && input.LumpSumMonth < 1)
            ModelState.AddModelError(nameof(input.LumpSumMonth), "Choose a valid month for the lump-sum payment.");
        if (input.FirstPaymentDate == default)
            ModelState.AddModelError(nameof(input.FirstPaymentDate), "Enter a first payment date.");
        if (debts.Select(x => x.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            ModelState.AddModelError(string.Empty, "Run scenarios separately for each currency.");

        if (ModelState.IsValid)
        {
            try { ViewBag.Comparison = Simulate(debts, input); }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }
        return View(input);
    }

    private async Task<List<DebtScenarioDebt>> LoadDebtsAsync(CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var debts = new List<DebtScenarioDebt>();
        foreach (var account in accounts)
        {
            var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            if (balance <= 0) continue;
            debts.Add(new DebtScenarioDebt
            {
                AccountId = account.Id, Name = account.Name, CurrencyCode = account.CurrencyCode,
                StartingBalance = decimal.Round(balance, 2, MidpointRounding.AwayFromZero),
                AnnualRatePercent = account.InterestRatePercent ?? 0m,
                MinimumPayment = account.MinimumPayment ?? 0m
            });
        }
        ViewBag.Debts = debts;
        return debts;
    }

    private static DebtScenarioComparison Simulate(IReadOnlyList<DebtScenarioDebt> debts, DebtScenarioInput input)
    {
        if (debts.Count == 0) throw new InvalidOperationException("At least one debt is required.");
        if (input.AprAdjustment > 100 || input.ExtraMonthlyBudget < 0 || input.LumpSumPayment < 0)
            throw new InvalidOperationException("Enter non-negative payment amounts and a valid APR adjustment.");

        var baseline = Run(debts, 0m, 0m, 1, 0m, input.FirstPaymentDate, "Current payment baseline");
        var scenarioDebts = debts.Select(d => new DebtScenarioDebt
        {
            AccountId = d.AccountId, Name = d.Name, CurrencyCode = d.CurrencyCode,
            StartingBalance = d.StartingBalance, MinimumPayment = d.MinimumPayment,
            AnnualRatePercent = Math.Clamp(d.AnnualRatePercent - input.AprAdjustment, 0m, 100m)
        }).ToList();

        var minimumBudget = scenarioDebts.Sum(d => Math.Max(d.MinimumPayment, Math.Max(1m, decimal.Round(d.StartingBalance * .01m, 2))));
        var budget = input.MonthlyBudgetOverride ?? (minimumBudget + input.ExtraMonthlyBudget);
        if (budget <= 0) throw new InvalidOperationException("The monthly payment budget must be greater than zero.");
        var snowball = Run(scenarioDebts, budget, input.LumpSumPayment, input.LumpSumMonth, input.AprAdjustment, input.FirstPaymentDate, "Snowball");
        var avalanche = Run(scenarioDebts, budget, input.LumpSumPayment, input.LumpSumMonth, input.AprAdjustment, input.FirstPaymentDate, "Avalanche");
        return new DebtScenarioComparison
        {
            Baseline = baseline, Snowball = snowball, Avalanche = avalanche,
            InterestSavingsVsBaseline = baseline.TotalInterest - Math.Min(snowball.TotalInterest, avalanche.TotalInterest),
            MonthsSavedVsBaseline = baseline.MonthsToPayoff - Math.Min(snowball.MonthsToPayoff, avalanche.MonthsToPayoff)
        };
    }

    private static DebtScenarioResult Run(IReadOnlyList<DebtScenarioDebt> debts, decimal budget,
        decimal lumpSum, int lumpMonth, decimal aprAdjustment, DateTime firstDate, string strategy)
    {
        var balances = debts.ToDictionary(d => d.AccountId, d => d.StartingBalance);
        var interestByDebt = debts.ToDictionary(d => d.AccountId, _ => 0m);
        var payoffMonths = new Dictionary<int, int>();
        var totalInterest = 0m;
        var totalPaid = 0m;
        var rows = new List<DebtScenarioPaymentRow>();
        var baselineMode = strategy == "Current payment baseline";

        for (var month = 1; month <= 1200 && balances.Values.Any(b => b > 0); month++)
        {
            var monthInterest = 0m;
            foreach (var debt in debts)
            {
                var balance = balances[debt.AccountId];
                if (balance <= 0) continue;
                var interest = decimal.Round(balance * debt.AnnualRatePercent / 1200m, 2, MidpointRounding.AwayFromZero);
                balances[debt.AccountId] += interest;
                interestByDebt[debt.AccountId] += interest;
                monthInterest += interest;
            }

            var remainingBudget = baselineMode
                ? debts.Where(d => balances[d.AccountId] > 0).Sum(d => Math.Min(balances[d.AccountId],
                    Math.Max(d.MinimumPayment, Math.Max(1m, decimal.Round(balances[d.AccountId] * .01m, 2)))))
                : budget;

            if (lumpSum > 0 && month == lumpMonth)
                remainingBudget += lumpSum;

            foreach (var debt in debts)
            {
                var balance = balances[debt.AccountId];
                if (balance <= 0) continue;
                var minimum = Math.Max(debt.MinimumPayment, Math.Max(1m, decimal.Round(balance * .01m, 2)));
                var payment = Math.Min(balance, Math.Min(minimum, remainingBudget));
                balances[debt.AccountId] -= payment;
                remainingBudget -= payment;
                totalPaid += payment;
                if (balances[debt.AccountId] <= 0) payoffMonths.TryAdd(debt.AccountId, month);
            }

            var priority = debts.Where(d => balances[d.AccountId] > 0);
            priority = strategy == "Avalanche"
                ? priority.OrderByDescending(d => d.AnnualRatePercent).ThenBy(d => balances[d.AccountId])
                : priority.OrderBy(d => balances[d.AccountId]).ThenByDescending(d => d.AnnualRatePercent);
            foreach (var debt in priority)
            {
                if (remainingBudget <= 0) break;
                var payment = Math.Min(balances[debt.AccountId], remainingBudget);
                balances[debt.AccountId] -= payment;
                remainingBudget -= payment;
                totalPaid += payment;
                if (balances[debt.AccountId] <= 0) payoffMonths.TryAdd(debt.AccountId, month);
            }
            foreach (var key in balances.Keys.ToList())
                if (balances[key] < 0.01m) balances[key] = 0m;

            totalInterest += monthInterest;
            rows.Add(new DebtScenarioPaymentRow
            {
                Month = month, PaymentDate = firstDate.Date.AddMonths(month - 1),
                Payment = totalPaid - rows.Sum(r => r.Payment), Interest = monthInterest,
                RemainingBalance = balances.Values.Sum()
            });
        }

        var complete = balances.Values.All(b => b <= 0);
        var months = rows.Count;
        return new DebtScenarioResult
        {
            Strategy = strategy, StartingBalance = debts.Sum(d => d.StartingBalance),
            MonthlyBudget = budget, TotalInterest = totalInterest, TotalPaid = totalPaid,
            MonthsToPayoff = complete ? months : 1200,
            DebtFreeDate = firstDate.Date.AddMonths((complete ? months : 1200) - 1),
            ReachedDebtFree = complete,
            Debts = debts.Select(d =>
            {
                var payoffMonth = payoffMonths.GetValueOrDefault(d.AccountId, complete ? months : 1200);
                return new DebtScenarioDebtResult
                {
                    AccountId = d.AccountId, Name = d.Name, MonthsToPayoff = payoffMonth,
                    PayoffDate = firstDate.Date.AddMonths(payoffMonth - 1),
                    InterestPaid = interestByDebt[d.AccountId]
                };
            }).ToList(),
            Payments = rows
        };
    }
}
