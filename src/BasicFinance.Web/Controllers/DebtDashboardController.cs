using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtDashboardController(
    ApplicationDbContext database,
    AccountBalanceService balances,
    DebtManagementService debtService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active &&
                (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .OrderBy(a => a.CurrencyCode).ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        var current = new Dictionary<int, decimal>();
        foreach (var account in accounts)
            current[account.Id] = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));

        var milestones = await database.DebtFreeMilestones.AsNoTracking()
            .Include(x => x.Account)
            .OrderByDescending(x => x.AchievedDate)
            .ToListAsync(cancellationToken);

        var model = new DebtDashboardViewModel
        {
            ActiveDebtAccountCount = accounts.Count(a => current[a.Id] > 0m),
            PaidOffAccountCount = milestones.Count(m => !m.IsReopened),
            RecentMilestones = milestones.Take(8).Select(m => new DebtDashboardMilestone(
                m.Account.Name, m.Account.CurrencyCode, m.AchievedDate, m.IsReopened, m.ReopenedDate)).ToList()
        };

        foreach (var group in accounts.GroupBy(a => a.CurrencyCode).OrderBy(g => g.Key))
        {
            var balance = group.Sum(a => current[a.Id]);
            var opening = group.Sum(a => Math.Max(0m, a.OpeningBalance > 0m ? a.OpeningBalance : a.OriginalLoanAmount ?? 0m));
            model.CurrencySummaries.Add(new DebtDashboardCurrencySummary(
                group.Key, balance, opening, opening - balance, group.Count(a => current[a.Id] > 0m),
                group.Where(a => current[a.Id] > 0m).Sum(a => a.MinimumPayment ?? 0m)));

            var debts = group.Where(a => current[a.Id] > 0m).ToList();
            if (debts.Count == 0) continue;
            var missing = debts.Where(a => !a.InterestRatePercent.HasValue ||
                !a.MinimumPayment.HasValue || a.MinimumPayment <= 0m).ToList();
            if (missing.Count > 0)
            {
                model.StrategyComparisons.Add(new DebtStrategyComparison(group.Key, balance,
                    null, null, 0m, 0m, false,
                    "Add APR and positive minimum payments for: " + string.Join(", ", missing.Select(a => a.Name))));
                continue;
            }

            var inputs = debts.Select(a => new DebtPayoffDebtInput
            {
                AccountId = a.Id,
                Name = a.Name,
                Balance = current[a.Id],
                AnnualRatePercent = a.InterestRatePercent!.Value,
                MinimumPayment = a.MinimumPayment!.Value
            }).ToList();
            try
            {
                var snowball = debtService.CalculatePayoff(inputs, 0m, avalanche: false,
                    startDate: DateTime.Today, applyMinimumPaymentFloor: false);
                var avalanche = debtService.CalculatePayoff(inputs, 0m, avalanche: true,
                    startDate: DateTime.Today, applyMinimumPaymentFloor: false);
                model.StrategyComparisons.Add(new DebtStrategyComparison(group.Key, balance,
                    snowball.ReachedDebtFree ? snowball.DebtFreeDate : null,
                    avalanche.ReachedDebtFree ? avalanche.DebtFreeDate : null,
                    snowball.TotalInterest, avalanche.TotalInterest,
                    snowball.ReachedDebtFree && avalanche.ReachedDebtFree,
                    snowball.ReachedDebtFree && avalanche.ReachedDebtFree ? null :
                        "Current minimum payments do not pay off all balances within the planning horizon."));
            }
            catch (InvalidOperationException ex)
            {
                model.StrategyComparisons.Add(new DebtStrategyComparison(group.Key, balance,
                    null, null, 0m, 0m, false, ex.Message));
            }
        }

        var accountIds = accounts.Select(a => a.Id).ToList();
        if (accountIds.Count > 0)
        {
            var snapshots = await database.AccountBalanceSnapshots.AsNoTracking()
                .Where(s => accountIds.Contains(s.AccountId))
                .OrderBy(s => s.SnapshotDate)
                .ToListAsync(cancellationToken);
            var currencyByAccount = accounts.ToDictionary(a => a.Id, a => a.CurrencyCode);
            model.BalanceTrends = snapshots
                .GroupBy(s => new { Currency = currencyByAccount[s.AccountId], Month = new DateTime(s.SnapshotDate.Year, s.SnapshotDate.Month, 1) })
                .SelectMany(monthGroup => monthGroup.GroupBy(s => s.AccountId)
                    .Select(accountGroup => accountGroup.OrderByDescending(s => s.SnapshotDate).First())
                    .GroupBy(_ => monthGroup.Key)
                    .Select(accountSnapshots => new DebtBalanceTrendPoint(
                        monthGroup.Key.Currency, monthGroup.Key.Month,
                        accountSnapshots.Sum(s => Math.Max(0m, s.Balance)), accountSnapshots.Count())))
                .OrderBy(x => x.CurrencyCode).ThenBy(x => x.Month)
                .ToList();
        }

        return View(model);
    }
}
