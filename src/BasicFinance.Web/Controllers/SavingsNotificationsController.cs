using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class SavingsNotificationsController(
    ApplicationDbContext database,
    SavingsNotificationService notificationService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await notificationService.GenerateAsync(DateTime.Today, cancellationToken);
        var preferences = await notificationService.GetPreferencesAsync(cancellationToken);
        var notifications = await database.SavingsNotifications.AsNoTracking()
            .OrderBy(x => x.IsRead).ThenByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);
        ViewBag.UnreadCount = notifications.Count(x => !x.IsRead);
        ViewBag.GeneratedAt = DateTime.Now;
        return View(new SavingsNotificationsViewModel
        {
            Preferences = preferences,
            Notifications = notifications
        });
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

        var preferences = await notificationService.GetPreferencesAsync(cancellationToken);
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
}

public sealed class SavingsNotificationsViewModel
{
    public SavingsNotificationPreference Preferences { get; set; } = new();
    public List<SavingsNotification> Notifications { get; set; } = [];
}
