using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BasicFinance.Web.Controllers;

public sealed class SavingsNotificationsController(
    ApplicationDbContext database,
    SavingsGoalService savingsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var preferences = await GetPreferencesAsync(cancellationToken);
        var alerts = new List<SavingsNotificationRow>();
        var today = DateTime.Today;

        var schedules = await database.RecurringSavingsContributions.AsNoTracking()
            .Include(x => x.SavingsGoal)
            .Where(x => x.IsActive && x.SavingsGoal.CurrencyCode == "USD"
                && x.SavingsGoal.Status == SavingsGoalStatus.Active)
            .OrderBy(x => x.NextDueDate)
            .ToListAsync(cancellationToken);

        foreach (var schedule in schedules)
        {
            if (schedule.EndDate.HasValue && schedule.NextDueDate.Date > schedule.EndDate.Value.Date)
                continue;

            var due = schedule.NextDueDate.Date;
            if (due < today && preferences.MissedContributionAlertsEnabled)
            {
                alerts.Add(new SavingsNotificationRow(
                    $"schedule-missed:{schedule.Id}:{due:yyyyMMdd}", "High", "Missed contribution",
                    $"{schedule.SavingsGoal.Name}: scheduled contribution of {schedule.Amount:C} was due {due:MMM d, yyyy} and has not been confirmed.", due));
            }
            else if (due >= today && due <= today.AddDays(preferences.UpcomingReminderDays)
                && preferences.UpcomingContributionRemindersEnabled)
            {
                alerts.Add(new SavingsNotificationRow(
                    $"schedule-upcoming:{schedule.Id}:{due:yyyyMMdd}", "Reminder", "Upcoming contribution",
                    $"{schedule.SavingsGoal.Name}: contribution of {schedule.Amount:C} is due {due:MMM d, yyyy}.", due));
            }
        }

        if (preferences.MilestoneNotificationsEnabled)
        {
            var recentMilestones = await database.SavingsGoalMilestones.AsNoTracking()
                .Include(x => x.SavingsGoal)
                .Where(x => x.AchievedUtc >= DateTime.UtcNow.AddDays(-30)
                    && x.SavingsGoal.CurrencyCode == "USD")
                .OrderByDescending(x => x.AchievedUtc)
                .Take(100).ToListAsync(cancellationToken);
            foreach (var milestone in recentMilestones)
            {
                alerts.Add(new SavingsNotificationRow(
                    $"milestone:{milestone.Id}", "Success", "Savings milestone",
                    $"{milestone.SavingsGoal.Name} reached {milestone.MilestonePercent}% of its savings target.", milestone.AchievedUtc.Date));
            }
        }

        if (preferences.EmergencyFundWarningsEnabled)
        {
            var plan = await database.EmergencyFundPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
            var emergencyGoal = await database.SavingsGoals.AsNoTracking()
                .Include(x => x.Contributions)
                .FirstOrDefaultAsync(x => x.IsEmergencyFund && x.CurrencyCode == "USD", cancellationToken);
            if (plan is not null && emergencyGoal is not null && plan.MonthlyEssentialExpenses > 0m)
            {
                var progress = savingsService.CalculateProgress(emergencyGoal, today);
                var coverage = savingsService.CalculateEmergencyFundCoverageMonths(
                    progress.CurrentAmount, plan.MonthlyEssentialExpenses);
                if (coverage < preferences.EmergencyFundWarningMonths)
                {
                    alerts.Add(new SavingsNotificationRow(
                        $"emergency-coverage:{today:yyyyMM}:{preferences.EmergencyFundWarningMonths:0.##}",
                        "High", "Emergency fund coverage",
                        $"Your emergency fund currently covers {coverage:0.0} months of essential expenses; your warning threshold is {preferences.EmergencyFundWarningMonths:0.0} months.", today));
                }
            }
        }

        if (preferences.MonthlyAllocationReviewsEnabled && today.Day <= 7)
        {
            var budget = await database.SavingsAllocationBudgets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
            var settings = await database.SavingsAllocationSettings.AsNoTracking()
                .Where(x => x.SavingsGoal.CurrencyCode == "USD" && x.SavingsGoal.Status == SavingsGoalStatus.Active)
                .Select(x => x.AllocationPercent).ToListAsync(cancellationToken);
            var allocated = settings.Sum();
            var monthlyBudget = budget?.MonthlyBudget ?? 0m;
            if (monthlyBudget > 0m || allocated > 0m)
            {
                var planned = decimal.Round(monthlyBudget * allocated / 100m, 2, MidpointRounding.AwayFromZero);
                var unallocated = Math.Max(0m, monthlyBudget - planned);
                alerts.Add(new SavingsNotificationRow(
                    $"allocation-review:{today:yyyyMM}", "Reminder", "Monthly allocation review",
                    $"Review this month's savings plan: {allocated:0.##}% allocated, {planned:C} planned, and {unallocated:C} unallocated.", today));
            }
        }

        foreach (var alert in alerts)
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(alert.Key)));
            var exists = await database.SavingsNotifications.AnyAsync(x => x.NotificationKey == key, cancellationToken);
            if (!exists)
            {
                database.SavingsNotifications.Add(new SavingsNotification
                {
                    NotificationKey = key,
                    Level = alert.Level,
                    Category = alert.Category,
                    Message = alert.Message,
                    EventDate = alert.EventDate,
                    CreatedUtc = DateTime.UtcNow
                });
            }
        }
        await database.SaveChangesAsync(cancellationToken);

        var notifications = await database.SavingsNotifications.AsNoTracking()
            .OrderBy(x => x.IsRead).ThenByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
        ViewBag.UnreadCount = notifications.Count(x => !x.IsRead);
        ViewBag.GeneratedAt = DateTime.Now;
        return View(new SavingsNotificationsViewModel { Preferences = preferences, Notifications = notifications });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePreferences(
        bool upcomingContributionRemindersEnabled,
        bool missedContributionAlertsEnabled,
        bool milestoneNotificationsEnabled,
        bool emergencyFundWarningsEnabled,
        bool monthlyAllocationReviewsEnabled,
        int upcomingReminderDays,
        decimal emergencyFundWarningMonths,
        CancellationToken cancellationToken)
    {
        if (upcomingReminderDays is < 0 or > 30 || emergencyFundWarningMonths is < 0.5m or > 24m)
        {
            TempData["ErrorMessage"] = "Reminder days must be 0–30 and emergency-fund warning coverage must be 0.5–24 months.";
            return RedirectToAction(nameof(Index));
        }

        var preferences = await GetPreferencesAsync(cancellationToken);
        preferences.UpcomingContributionRemindersEnabled = upcomingContributionRemindersEnabled;
        preferences.MissedContributionAlertsEnabled = missedContributionAlertsEnabled;
        preferences.MilestoneNotificationsEnabled = milestoneNotificationsEnabled;
        preferences.EmergencyFundWarningsEnabled = emergencyFundWarningsEnabled;
        preferences.MonthlyAllocationReviewsEnabled = monthlyAllocationReviewsEnabled;
        preferences.UpcomingReminderDays = upcomingReminderDays;
        preferences.EmergencyFundWarningMonths = emergencyFundWarningMonths;
        preferences.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Savings notification preferences saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var notification = await database.SavingsNotifications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notification is null) return NotFound();
        notification.IsRead = true;
        await database.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await database.SavingsNotifications.Where(x => !x.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true), cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<SavingsNotificationPreference> GetPreferencesAsync(CancellationToken cancellationToken)
    {
        var preferences = await database.SavingsNotificationPreferences
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (preferences is not null) return preferences;
        preferences = new SavingsNotificationPreference { Id = 1 };
        database.SavingsNotificationPreferences.Add(preferences);
        await database.SaveChangesAsync(cancellationToken);
        return preferences;
    }
}

public sealed class SavingsNotificationsViewModel
{
    public SavingsNotificationPreference Preferences { get; set; } = new();
    public List<SavingsNotification> Notifications { get; set; } = [];
}

public sealed record SavingsNotificationRow(string Key, string Level, string Category, string Message, DateTime EventDate);
