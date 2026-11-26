using BasicFinance.Web.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BasicFinance.Tests;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task Migrate_CreatesAccountDebtAndTransactionCategoryTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var expectedTables = new[]
        {
            "Accounts", "Transactions", "StatementImports", "StatementImportRows",
            "TransactionCategories", "TransactionCategoryMappings", "TransactionRules",
            "AccountBalanceSnapshots", "InterestRecords", "DebtToIncomeRecords", "DebtPaymentRecords",
            "CreditLimitHistory", "ReportedBalanceSnapshots", "CreditInquiries",
            "DebtFreeTargets", "MonthlyDebtReductionGoals", "DebtFreeMilestones",
            "DebtNotifications", "DebtInterestRateHistory", "RecurringCashFlows",
            "CashFlowAlertPreferences", "CashFlowAlerts", "SavingsGoals", "SavingsContributions",
            "EmergencyFundPlans", "SavingsMonthlyTargets", "SavingsGoalMilestones",
            "RecurringSavingsContributions", "SavingsAllocationBudgets", "SavingsAllocationSettings",
            "SavingsNotificationPreferences", "SavingsNotifications",
            "SavingsReconciliationSessions", "SavingsReconciliationItems"
        };

        foreach (var table in expectedTables)
        {
            var exists = await database.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {0}", table)
                .SingleAsync();
            Assert.Equal(1, exists);
        }
    }

    [Fact]
    public async Task Migrate_EnforcesUniqueSavingsNotificationKeys()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var uniqueNotificationKeyIndex = false;
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA index_list('SavingsNotifications')";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1) == "IX_SavingsNotifications_NotificationKey")
                uniqueNotificationKeyIndex = reader.GetInt32(2) == 1;
        }

        Assert.True(uniqueNotificationKeyIndex);
    }

    [Fact]
    public async Task Migrate_EnforcesOneRecurringSavingsEntryPerDueDate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var hasScheduleLink = false;
        await using (var columnCommand = connection.CreateCommand())
        {
            columnCommand.CommandText = "PRAGMA table_info('SavingsContributions')";
            await using var reader = await columnCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == "RecurringSavingsContributionId")
                    hasScheduleLink = true;
            }
        }

        var uniqueOccurrenceIndex = false;
        await using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.CommandText = "PRAGMA index_list('SavingsContributions')";
            await using var reader = await indexCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == "IX_SavingsContributions_RecurringSavingsContributionId_ScheduledDueDate")
                    uniqueOccurrenceIndex = reader.GetInt32(2) == 1;
            }
        }

        Assert.True(hasScheduleLink);
        Assert.True(uniqueOccurrenceIndex);
    }

    [Fact]
    public async Task Migrate_CreatesDebtNotificationUniquenessAndAprHistoryRelationship()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var notificationIndexIsUnique = false;
        await using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.CommandText = "PRAGMA index_list('DebtNotifications')";
            await using var reader = await indexCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1) == "IX_DebtNotifications_NotificationKey")
                    notificationIndexIsUnique = reader.GetInt32(2) == 1;
            }
        }

        Assert.True(notificationIndexIsUnique);

        var hasAccountForeignKey = false;
        await using (var foreignKeyCommand = connection.CreateCommand())
        {
            foreignKeyCommand.CommandText = "PRAGMA foreign_key_list('DebtInterestRateHistory')";
            await using var reader = await foreignKeyCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(2) == "Accounts" &&
                    reader.GetString(3) == "AccountId" &&
                    reader.GetString(4) == "Id" &&
                    reader.GetString(6).Equals("RESTRICT", StringComparison.OrdinalIgnoreCase))
                {
                    hasAccountForeignKey = true;
                }
            }
        }

        Assert.True(hasAccountForeignKey);
    }

}
