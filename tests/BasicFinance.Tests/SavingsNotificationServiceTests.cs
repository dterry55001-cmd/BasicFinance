using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BasicFinance.Tests;

public sealed class SavingsNotificationServiceTests
{
    [Fact]
    public async Task GenerateAsync_CreatesUpcomingReminderOnlyOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var goal = new SavingsGoal
        {
            Name = "Emergency reserve",
            TargetAmount = 5000m,
            CurrencyCode = "USD",
            StartDate = new DateTime(2026, 1, 1),
            Status = SavingsGoalStatus.Active
        };
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();
        database.RecurringSavingsContributions.Add(new RecurringSavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 100m,
            Frequency = SavingsScheduleFrequency.Monthly,
            NextDueDate = new DateTime(2026, 11, 28),
            IsActive = true
        });
        await database.SaveChangesAsync();

        var service = new SavingsNotificationService(database, new SavingsGoalService());
        var referenceDate = new DateTime(2026, 11, 26);

        var firstCount = await service.GenerateAsync(referenceDate);
        var secondCount = await service.GenerateAsync(referenceDate);

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount);
        var notification = await database.SavingsNotifications.SingleAsync();
        Assert.Equal("Upcoming contribution", notification.Category);
        Assert.Contains("Emergency reserve", notification.Message);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task GenerateAsync_CreatesMissedContributionAlertAndRespectsPreference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var goal = new SavingsGoal
        {
            Name = "Car fund",
            TargetAmount = 3000m,
            CurrencyCode = "USD",
            StartDate = new DateTime(2026, 1, 1),
            Status = SavingsGoalStatus.Active
        };
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();
        database.RecurringSavingsContributions.Add(new RecurringSavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 75m,
            Frequency = SavingsScheduleFrequency.Weekly,
            NextDueDate = new DateTime(2026, 11, 20),
            IsActive = true
        });
        await database.SaveChangesAsync();

        var service = new SavingsNotificationService(database, new SavingsGoalService());
        var referenceDate = new DateTime(2026, 11, 26);

        Assert.Equal(1, await service.GenerateAsync(referenceDate));
        Assert.Contains("Missed contribution", (await database.SavingsNotifications.SingleAsync()).Category);

        var preferences = await service.GetPreferencesAsync();
        preferences.MissedContributionAlertsEnabled = false;
        await database.SaveChangesAsync();

        Assert.Equal(0, await service.GenerateAsync(referenceDate));
        Assert.Single(await database.SavingsNotifications.ToListAsync());
    }

    [Fact]
    public async Task GenerateAsync_DoesNotRemindForSchedulePastItsEndDate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var goal = new SavingsGoal
        {
            Name = "Holiday",
            TargetAmount = 1200m,
            CurrencyCode = "USD",
            StartDate = new DateTime(2026, 1, 1),
            Status = SavingsGoalStatus.Active
        };
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();
        database.RecurringSavingsContributions.Add(new RecurringSavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 50m,
            Frequency = SavingsScheduleFrequency.Monthly,
            NextDueDate = new DateTime(2026, 11, 20),
            EndDate = new DateTime(2026, 11, 15),
            IsActive = true
        });
        await database.SaveChangesAsync();

        var service = new SavingsNotificationService(database, new SavingsGoalService());

        Assert.Equal(0, await service.GenerateAsync(new DateTime(2026, 11, 26)));
        Assert.Empty(await database.SavingsNotifications.ToListAsync());
    }
}
