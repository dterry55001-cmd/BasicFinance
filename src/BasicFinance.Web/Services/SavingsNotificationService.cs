using System.Security.Cryptography;
using System.Text;
using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

public sealed class SavingsNotificationService(
    ApplicationDbContext database,
    SavingsGoalService savingsService)
{
    public async Task<SavingsNotificationPreference> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var preferences = await database.SavingsNotificationPreferences
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (preferences is not null) return preferences;

        preferences = new SavingsNotificationPreference { Id = 1 };
        database.SavingsNotificationPreferences.Add(preferences);
        await database.SaveChangesAsync(cancellationToken);
        return preferences;
    }


    public async Task CreateReconciliationExceptionAlertAsync(
        int sessionId,
        string goalName,
        DateTime periodEnd,
        int unmatchedTransactions,
        int unmatchedContributions,
        int potentialDuplicates,
        CancellationToken cancellationToken = default)
    {
        var exceptionCount = unmatchedTransactions + unmatchedContributions + potentialDuplicates;
        if (exceptionCount == 0) return;

        var rawKey = $"reconciliation-exceptions:{sessionId}";
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
        if (await database.SavingsNotifications.AnyAsync(x => x.NotificationKey == key, cancellationToken))
            return;

        database.SavingsNotifications.Add(new SavingsNotification
        {
            NotificationKey = key,
            Level = "Warning",
            Category = "Reconciliation exceptions",
            Message = $"Reconciliation session #{sessionId} for {goalName}: {unmatchedTransactions} unmatched transaction(s), {unmatchedContributions} unmatched savings entry/entries, and {potentialDuplicates} potential duplicate(s). Review this session in reconciliation history.",
            EventDate = periodEnd.Date,
            CreatedUtc = DateTime.UtcNow
        });

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            foreach (var entry in database.ChangeTracker.Entries<SavingsNotification>()
                .Where(x => x.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
        }
    }

    public async Task<int> GenerateAsync(DateTime referenceDate, CancellationToken cancellationToken = default)
    {
        var preferences = await GetPreferencesAsync(cancellationToken);
        var today = referenceDate.Date;
        var alerts = new List<SavingsNotificationRow>();

        if (preferences.UpcomingContributionRemindersEnabled || preferences.MissedContributionAlertsEnabled)
        {
            var schedules = await database.RecurringSavingsContributions.AsNoTracking()
                .Include(x => x.SavingsGoal)
                .Where(x => x.IsActive && x.SavingsGoal.CurrencyCode == "USD"
                    && x.SavingsGoal.Status == SavingsGoalStatus.Active)
                .OrderBy(x => x.NextDueDate)
                .ToListAsync(cancellationToken);

            foreach (var schedule in schedules)
            {
                var due = schedule.NextDueDate.Date;
                if (schedule.EndDate.HasValue && due > schedule.EndDate.Value.Date) continue;

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
        }

        if (preferences.MilestoneNotificationsEnabled)
        {
            var recentMilestones = await database.SavingsGoalMilestones.AsNoTracking()
                .Include(x => x.SavingsGoal)
                .Where(x => x.AchievedUtc >= DateTime.UtcNow.AddDays(-30)
                    && x.SavingsGoal.CurrencyCode == "USD")
                .OrderByDescending(x => x.AchievedUtc).Take(100)
                .ToListAsync(cancellationToken);
            foreach (var milestone in recentMilestones)
            {
                alerts.Add(new SavingsNotificationRow(
                    $"milestone:{milestone.Id}", "Success", "Savings milestone",
                    $"{milestone.SavingsGoal.Name} reached {milestone.MilestonePercent}% of its savings target.",
                    milestone.AchievedUtc.Date));
            }
        }

        if (preferences.EmergencyFundWarningsEnabled)
        {
            var plan = await database.EmergencyFundPlans.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
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
                        $"Your emergency fund currently covers {coverage:0.0} months of essential expenses; your warning threshold is {preferences.EmergencyFundWarningMonths:0.0} months.",
                        today));
                }
            }
        }

        if (preferences.MonthlyAllocationReviewsEnabled && today.Day <= 7)
        {
            var budget = await database.SavingsAllocationBudgets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
            var percentages = await database.SavingsAllocationSettings.AsNoTracking()
                .Where(x => x.SavingsGoal.CurrencyCode == "USD" && x.SavingsGoal.Status == SavingsGoalStatus.Active)
                .Select(x => x.AllocationPercent).ToListAsync(cancellationToken);
            var allocated = percentages.Sum();
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

        var created = 0;
        foreach (var alert in alerts)
        {
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(alert.Key)));
            if (await database.SavingsNotifications.AnyAsync(x => x.NotificationKey == key, cancellationToken))
                continue;

            database.SavingsNotifications.Add(new SavingsNotification
            {
                NotificationKey = key,
                Level = alert.Level,
                Category = alert.Category,
                Message = alert.Message,
                EventDate = alert.EventDate,
                CreatedUtc = DateTime.UtcNow
            });

            try
            {
                await database.SaveChangesAsync(cancellationToken);
                created++;
            }
            catch (DbUpdateException)
            {
                // A parallel request or worker may have inserted the same deterministic key.
                foreach (var entry in database.ChangeTracker.Entries<SavingsNotification>()
                    .Where(x => x.State == EntityState.Added).ToList())
                    entry.State = EntityState.Detached;
            }
        }

        return created;
    }
}

public sealed record SavingsNotificationRow(string Key, string Level, string Category, string Message, DateTime EventDate);
