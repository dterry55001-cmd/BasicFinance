using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtGoalsController(
    ApplicationDbContext database,
    AccountBalanceService balances,
    DebtManagementService debtService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await RefreshMilestones(cancellationToken);
        var accounts = await ActiveDebts().AsNoTracking().OrderBy(a => a.CurrencyCode).ThenBy(a => a.Name).ToListAsync(cancellationToken);
        var currentBalances = new Dictionary<int, decimal>();
        foreach (var account in accounts)
            currentBalances[account.Id] = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));

        var targets = await database.DebtFreeTargets.AsNoTracking().OrderBy(x => x.CurrencyCode).ToListAsync(cancellationToken);
        var goals = await database.MonthlyDebtReductionGoals.AsNoTracking()
            .OrderByDescending(x => x.Month).Take(36).ToListAsync(cancellationToken);
        var model = new DebtGoalsPageViewModel
        {
            Targets = targets,
            Milestones = await database.DebtFreeMilestones.AsNoTracking().Include(x => x.Account)
                .OrderByDescending(x => x.AchievedDate).ToListAsync(cancellationToken),
            Balances = accounts.GroupBy(a => a.CurrencyCode).Select(g =>
                new DebtCurrencyBalance(g.Key, g.Sum(a => currentBalances[a.Id]), g.Count())).ToList()
        };

        foreach (var goal in goals)
        {
            var monthStart = new DateTime(goal.Month.Year, goal.Month.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var asOf = DateTime.Today < monthEnd ? DateTime.Today : monthEnd;
            var currencyAccounts = accounts.Where(a => a.CurrencyCode == goal.CurrencyCode).ToList();
            decimal opening = 0m, closing = 0m;
            foreach (var account in currencyAccounts)
            {
                opening += Math.Max(0m, await balances.GetBalanceAtAsync(account.Id, monthStart.AddTicks(-1), cancellationToken));
                closing += Math.Max(0m, await balances.GetBalanceAtAsync(account.Id, asOf, cancellationToken));
            }
            var reduction = opening - closing;
            model.MonthlyGoals.Add(new MonthlyDebtGoalProgress {
                Goal = goal, OpeningBalance = opening, ClosingBalance = closing,
                ActualReduction = reduction, RemainingToGoal = goal.TargetReduction - reduction
            });
        }

        var targetByCurrency = targets.ToDictionary(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase);
        foreach (var group in accounts.GroupBy(a => a.CurrencyCode))
        {
            var total = group.Sum(a => currentBalances[a.Id]);
            if (total <= 0m) continue;
            var estimate = new DebtPayoffEstimateRow {
                CurrencyCode = group.Key, TotalBalance = total,
                TargetDebtFreeDate = targetByCurrency.GetValueOrDefault(group.Key)?.TargetDate
            };
            var missing = group.Where(a => currentBalances[a.Id] > 0m &&
                (!a.InterestRatePercent.HasValue || !a.MinimumPayment.HasValue || a.MinimumPayment <= 0m))
                .Select(a => $"{a.Name} ({string.Join(", ", new[] { !a.InterestRatePercent.HasValue ? "APR" : null, !a.MinimumPayment.HasValue || a.MinimumPayment <= 0m ? "minimum payment" : null }.Where(x => x is not null))})")
                .ToList();
            if (missing.Count > 0)
                estimate.MissingInputs = "Enter missing APR and/or minimum payment for: " + string.Join("; ", missing);
            else
            {
                try
                {
                    var extra = targetByCurrency.GetValueOrDefault(group.Key)?.ExtraMonthlyPayment ?? 0m;
                    var debts = group.Where(a => currentBalances[a.Id] > 0m).Select(a => new DebtPayoffDebtInput {
                        AccountId = a.Id, Name = a.Name, Balance = currentBalances[a.Id],
                        AnnualRatePercent = a.InterestRatePercent!.Value, MinimumPayment = a.MinimumPayment!.Value
                    }).ToList();
                    var plan = debtService.CalculatePayoff(debts, extra, avalanche: true, startDate: DateTime.Today);
                    if (plan.ReachedDebtFree)
                    {
                        estimate.EstimatedDebtFreeDate = plan.DebtFreeDate;
                        estimate.MonthsRemaining = plan.MonthsToPayoff;
                        estimate.EstimatedInterest = plan.TotalInterest;
                        estimate.IsComplete = true;
                    }
                    else estimate.MissingInputs = "The current payment plan does not reach a debt-free balance within 1,200 months. Increase minimum or extra payments.";
                }
                catch (InvalidOperationException ex) { estimate.MissingInputs = ex.Message; }
            }
            model.Estimates.Add(estimate);
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTarget(string currencyCode, DateTime targetDate, decimal extraMonthlyPayment, string? notes, CancellationToken cancellationToken)
    {
        var currency = NormalizeCurrency(currencyCode);
        if (currency is null || targetDate == default || extraMonthlyPayment < 0 || extraMonthlyPayment > 999999999999m)
        {
            TempData["ErrorMessage"] = "Enter a valid three-letter currency, target date, and non-negative extra monthly payment.";
            return RedirectToAction(nameof(Index));
        }
        var target = await database.DebtFreeTargets.FirstOrDefaultAsync(x => x.CurrencyCode == currency, cancellationToken);
        if (target is null)
        {
            target = new DebtFreeTarget { CurrencyCode = currency, TargetDate = targetDate.Date, ExtraMonthlyPayment = extraMonthlyPayment, Notes = Clean(notes) };
            database.DebtFreeTargets.Add(target);
        }
        else
        {
            target.TargetDate = targetDate.Date;
            target.ExtraMonthlyPayment = extraMonthlyPayment;
            target.Notes = Clean(notes);
            target.UpdatedUtc = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Debt-free target saved for {currency}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMonthlyGoal(string currencyCode, DateTime month, decimal targetReduction, string? notes, CancellationToken cancellationToken)
    {
        var currency = NormalizeCurrency(currencyCode);
        if (currency is null || month == default || targetReduction <= 0 || targetReduction > 999999999999m)
        {
            TempData["ErrorMessage"] = "Enter a valid currency, month, and positive monthly reduction goal.";
            return RedirectToAction(nameof(Index));
        }
        var monthStart = new DateTime(month.Year, month.Month, 1);
        var goal = await database.MonthlyDebtReductionGoals.FirstOrDefaultAsync(x => x.CurrencyCode == currency && x.Month == monthStart, cancellationToken);
        if (goal is null)
        {
            database.MonthlyDebtReductionGoals.Add(new MonthlyDebtReductionGoal {
                CurrencyCode = currency, Month = monthStart, TargetReduction = targetReduction, Notes = Clean(notes)
            });
        }
        else
        {
            goal.TargetReduction = targetReduction;
            goal.Notes = Clean(notes);
            goal.UpdatedUtc = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Monthly debt reduction goal saved for {currency}.";
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Account> ActiveDebts() => database.Accounts.Where(a => a.Status == AccountStatus.Active &&
        (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard));

    private async Task RefreshMilestones(CancellationToken cancellationToken)
    {
        var accounts = await ActiveDebts().ToListAsync(cancellationToken);
        foreach (var account in accounts)
        {
            var current = await balances.GetBalanceAsync(account.Id, cancellationToken);
            var milestone = await database.DebtFreeMilestones.FirstOrDefaultAsync(x => x.AccountId == account.Id, cancellationToken);
            if (current > 0m)
            {
                if (milestone is { IsReopened: false })
                {
                    milestone.IsReopened = true;
                    milestone.ReopenedDate = DateTime.Today;
                }
                continue;
            }
            if (milestone is not null)
            {
                // Preserve the historical reopened flag even if the balance is paid off again.
                continue;
            }

            var hasDebtHistory = account.OpeningBalance > 0m || account.OriginalLoanAmount > 0m ||
                await database.AccountBalanceSnapshots.AnyAsync(x => x.AccountId == account.Id && x.Balance > 0m, cancellationToken) ||
                await database.DebtPaymentRecords.AnyAsync(x => x.AccountId == account.Id && x.Status == DebtPaymentStatus.Paid && (x.PrincipalPaid ?? 0m) > 0m, cancellationToken);
            if (!hasDebtHistory) continue;

            var dates = await database.Transactions.AsNoTracking()
                .Where(x => x.AccountId == account.Id || x.RelatedAccountId == account.Id)
                .Select(x => x.TransactionDate).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
            var achieved = DateTime.Today;
            foreach (var date in dates)
            {
                if (await balances.GetBalanceAtAsync(account.Id, date, cancellationToken) <= 0m)
                {
                    achieved = date.Date;
                    break;
                }
            }
            database.DebtFreeMilestones.Add(new DebtFreeMilestone {
                AccountId = account.Id, AchievedDate = achieved, CreatedUtc = DateTime.UtcNow
            });
        }
        if (database.ChangeTracker.HasChanges()) await database.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeCurrency(string? value)
    {
        var currency = (value ?? string.Empty).Trim().ToUpperInvariant();
        return currency.Length == 3 && currency.All(char.IsAsciiLetter) ? currency : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
