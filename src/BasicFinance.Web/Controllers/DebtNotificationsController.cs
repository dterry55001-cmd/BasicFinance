using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtNotificationsController(
    ApplicationDbContext database,
    AccountBalanceService balances) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var alerts = await BuildAlertsAsync(cancellationToken);
        foreach (var alert in alerts)
        {
            var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{alert.Level}|{alert.Category}|{alert.Message}|{alert.Date:O}")));
            var exists = await database.DebtNotifications.AnyAsync(x => x.NotificationKey == key, cancellationToken);
            if (!exists)
            {
                database.DebtNotifications.Add(new DebtNotification
                {
                    NotificationKey = key,
                    Level = alert.Level,
                    Category = alert.Category,
                    Message = alert.Message,
                    EventDate = alert.Date,
                    CreatedUtc = DateTime.UtcNow
                });
            }
        }
        await database.SaveChangesAsync(cancellationToken);

        var notifications = await database.DebtNotifications.AsNoTracking()
            .OrderBy(x => x.IsRead)
            .ThenByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
        ViewBag.GeneratedAt = DateTime.Now;
        ViewBag.UnreadCount = notifications.Count(x => !x.IsRead);
        return View(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var notification = await database.DebtNotifications
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null) return NotFound();
        notification.IsRead = true;
        await database.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await database.DebtNotifications
            .Where(x => !x.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<DebtNotificationRow>> BuildAlertsAsync(CancellationToken cancellationToken)
    {
        var alerts = new List<DebtNotificationRow>();
        var today = DateTime.Today;
        var revolving = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active &&
                (a.Type == AccountType.CreditCard ||
                 a.DebtType == DebtAccountType.CreditCard ||
                 a.DebtType == DebtAccountType.LineOfCredit ||
                 a.DebtType == DebtAccountType.HomeEquityLineOfCredit))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);

        foreach (var account in revolving)
        {
            if (account.CreditLimit is not > 0m) continue;
            var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            var utilization = balance / account.CreditLimit.Value * 100m;
            var level = utilization >= 90m ? "Critical" :
                utilization >= 75m ? "High" :
                utilization >= 50m ? "Elevated" :
                utilization >= 30m ? "Watch" : null;
            if (level is not null)
                alerts.Add(new DebtNotificationRow(level,
                    $"{account.Name}: credit utilization is {utilization:0.0}%.",
                    "Credit utilization", today));
        }

        var recentLimitChanges = await database.CreditLimitHistory.AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.EffectiveDate >= today.AddDays(-30))
            .OrderByDescending(x => x.EffectiveDate).Take(50).ToListAsync(cancellationToken);
        foreach (var change in recentLimitChanges)
        {
            var direction = change.PreviousLimit.HasValue
                ? change.NewLimit > change.PreviousLimit.Value ? "increased" :
                  change.NewLimit < change.PreviousLimit.Value ? "decreased" : "did not change"
                : "was recorded";
            alerts.Add(new DebtNotificationRow("Information",
                $"{change.Account.Name}: credit limit {direction} to {change.NewLimit:N2} {change.Account.CurrencyCode}.",
                "Credit limit change", change.EffectiveDate));
        }

        var targets = await database.DebtFreeTargets.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var target in targets.Where(x => x.TargetDate.Date >= today && x.TargetDate.Date <= today.AddDays(30)))
            alerts.Add(new DebtNotificationRow("Reminder",
                $"Your {target.CurrencyCode} debt-free target is {target.TargetDate:MMM d, yyyy}.",
                "Debt-free target", target.TargetDate));

        var monthStart = new DateTime(today.Year, today.Month, 1);
        var goals = await database.MonthlyDebtReductionGoals.AsNoTracking()
            .Where(x => x.Month == monthStart).ToListAsync(cancellationToken);
        foreach (var goal in goals)
        {
            var accounts = await database.Accounts.AsNoTracking()
                .Where(a => a.Status == AccountStatus.Active && a.CurrencyCode == goal.CurrencyCode &&
                    (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
                .ToListAsync(cancellationToken);
            decimal opening = 0m, current = 0m;
            foreach (var account in accounts)
            {
                opening += Math.Max(0m, await balances.GetBalanceAtAsync(account.Id, monthStart.AddTicks(-1), cancellationToken));
                current += Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            }
            var actual = opening - current;
            var remaining = goal.TargetReduction - actual;
            if (remaining > 0m)
                alerts.Add(new DebtNotificationRow("Progress",
                    $"{goal.CurrencyCode} monthly reduction goal: {actual:N2} of {goal.TargetReduction:N2} achieved; {remaining:N2} remaining.",
                    "Monthly debt goal", monthStart));
            else
                alerts.Add(new DebtNotificationRow("Success",
                    $"{goal.CurrencyCode} monthly debt reduction goal has been met or exceeded ({actual:N2} achieved).",
                    "Monthly debt goal", today));
        }

        var milestones = await database.DebtFreeMilestones.AsNoTracking().Include(x => x.Account)
            .Where(x => x.AchievedDate >= today.AddDays(-30))
            .OrderByDescending(x => x.AchievedDate).Take(50).ToListAsync(cancellationToken);
        foreach (var milestone in milestones)
            alerts.Add(new DebtNotificationRow("Success",
                $"{milestone.Account.Name} reached a debt-free milestone on {milestone.AchievedDate:MMM d, yyyy}.",
                "Debt-free milestone", milestone.AchievedDate));

        // Payment deadlines and overdue obligations.
        var scheduledPayments = await database.DebtPaymentRecords.AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.Status == DebtPaymentStatus.Scheduled && x.Account.Status == AccountStatus.Active &&
                x.DueDate.Date <= today.AddDays(7))
            .OrderBy(x => x.DueDate).Take(100).ToListAsync(cancellationToken);
        foreach (var payment in scheduledPayments)
        {
            var overdue = payment.DueDate.Date < today;
            alerts.Add(new DebtNotificationRow(overdue ? "Critical" : "Reminder",
                overdue
                    ? $"{payment.Account.Name}: scheduled payment of {payment.ExpectedAmount:N2} {payment.Account.CurrencyCode} was due {payment.DueDate:MMM d, yyyy} and is still marked scheduled."
                    : $"{payment.Account.Name}: payment of {payment.ExpectedAmount:N2} {payment.Account.CurrencyCode} is due {payment.DueDate:MMM d, yyyy}.",
                overdue ? "Overdue payment risk" : "Upcoming payment", payment.DueDate));
        }

        // Compare recent recorded balance snapshots with the preceding 30-day period.
        var snapshotStart = today.AddDays(-60);
        var snapshots = await database.AccountBalanceSnapshots.AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.SnapshotDate >= snapshotStart && x.Account.Status == AccountStatus.Active &&
                (x.Account.Type == AccountType.Debt || x.Account.Type == AccountType.CreditCard))
            .OrderBy(x => x.SnapshotDate).ToListAsync(cancellationToken);
        foreach (var group in snapshots.GroupBy(x => x.AccountId))
        {
            var ordered = group.OrderBy(x => x.SnapshotDate).ToList();
            var previous = ordered.Where(x => x.SnapshotDate.Date < today.AddDays(-30))
                .OrderByDescending(x => x.SnapshotDate).FirstOrDefault();
            var recent = ordered.Where(x => x.SnapshotDate.Date >= today.AddDays(-30))
                .OrderByDescending(x => x.SnapshotDate).FirstOrDefault();
            if (previous is null || recent is null || previous.Balance <= 0m) continue;
            var increase = recent.Balance - previous.Balance;
            if (increase <= Math.Max(25m, previous.Balance * 0.05m)) continue;
            alerts.Add(new DebtNotificationRow("Elevated",
                $"{recent.Account.Name}: recorded balance increased by {increase:N2} {recent.Account.CurrencyCode} over the latest 30-day comparison.",
                "Rising debt balance", recent.SnapshotDate));
        }

        // Highlight high contractual APRs without treating the threshold as a credit-score rule.
        var highRateAccounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active &&
                (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard) &&
                a.InterestRatePercent >= 20m)
            .OrderByDescending(a => a.InterestRatePercent).Take(50).ToListAsync(cancellationToken);
        foreach (var account in highRateAccounts)
        {
            var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            if (balance <= 0m) continue;
            alerts.Add(new DebtNotificationRow("High",
                $"{account.Name}: recorded APR is {account.InterestRatePercent:0.##}%; outstanding balance is {balance:N2} {account.CurrencyCode}.",
                "High-interest debt", today));
        }

        // Use the latest saved DTI record for each currency and surface deteriorating affordability.
        var latestDti = await database.DebtToIncomeRecords.AsNoTracking()
            .GroupBy(x => x.CurrencyCode)
            .Select(g => g.OrderByDescending(x => x.Month).First())
            .ToListAsync(cancellationToken);
        foreach (var record in latestDti)
        {
            if (record.TotalDebtRatioPercent >= 43m)
                alerts.Add(new DebtNotificationRow("High",
                    $"{record.CurrencyCode}: recorded debt-to-income ratio is {record.TotalDebtRatioPercent:0.0}%. Review the latest income and obligation figures.",
                    "Debt-to-income risk", record.Month));
            if (record.NetMonthlyIncome > 0m && record.MonthlyIncomeAfterObligations < 0m)
                alerts.Add(new DebtNotificationRow("Critical",
                    $"{record.CurrencyCode}: recorded monthly debt obligations and essential expenses exceed net income by {Math.Abs(record.MonthlyIncomeAfterObligations):N2}.",
                    "Cash-flow stress", record.Month));
            else if (record.NetMonthlyIncome > 0m && record.MonthlyIncomeAfterObligations < record.NetMonthlyIncome * 0.10m)
                alerts.Add(new DebtNotificationRow("Elevated",
                    $"{record.CurrencyCode}: less than 10% of recorded net income remains after listed debt obligations and essential expenses.",
                    "Limited cash-flow flexibility", record.Month));
        }

        return alerts;
    }

    private static int Priority(string level) => level switch
    {
        "Critical" => 5, "High" => 4, "Elevated" => 3, "Watch" => 2,
        "Reminder" => 2, "Progress" => 1, "Success" => 0, _ => 1
    };
}

public sealed record DebtNotificationRow(string Level, string Message, string Category, DateTime Date);
