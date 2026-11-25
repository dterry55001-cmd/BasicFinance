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

        return alerts;
    }

    private static int Priority(string level) => level switch
    {
        "Critical" => 5, "High" => 4, "Elevated" => 3, "Watch" => 2,
        "Reminder" => 2, "Progress" => 1, "Success" => 0, _ => 1
    };
}

public sealed record DebtNotificationRow(string Level, string Message, string Category, DateTime Date);
