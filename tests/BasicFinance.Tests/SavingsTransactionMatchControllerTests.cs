using BasicFinance.Web.Controllers;
using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BasicFinance.Tests;

public sealed class SavingsTransactionMatchControllerTests
{
    [Fact]
    public async Task Index_SuggestsUniqueTransferMatchForLinkedSavingsAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var checking = NewAccount("Checking", AccountType.Checking);
        var savings = NewAccount("Savings", AccountType.Savings);
        database.Accounts.AddRange(checking, savings);
        await database.SaveChangesAsync();
        var goal = NewGoal(savings.Id);
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var date = new DateTime(2026, 11, 20);
        var contribution = new SavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 125m,
            Type = SavingsContributionType.Contribution,
            ContributionDate = date
        };
        database.SavingsContributions.Add(contribution);
        database.Transactions.Add(new FinancialTransaction
        {
            AccountId = checking.Id,
            RelatedAccountId = savings.Id,
            Type = TransactionType.Transfer,
            Description = "Transfer to savings",
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = date
        });
        await database.SaveChangesAsync();

        var controller = new SavingsGoalsController(database, new SavingsGoalService(), new SavingsGoalProjectionCalculator());
        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var model = Assert.IsType<SavingsGoalsIndexViewModel>(result.Model);

        var suggestion = Assert.Single(model.MatchingSuggestions);
        Assert.Equal(contribution.Id, suggestion.ContributionId);
        Assert.Equal("Emergency reserve", suggestion.GoalName);
        Assert.Equal(125m, suggestion.TransactionAmount);
    }


    [Fact]
    public async Task Index_SuggestsUniqueBankTransactionForDueRecurringSchedule()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var checking = NewAccount("Checking", AccountType.Checking);
        var savings = NewAccount("Savings", AccountType.Savings);
        database.Accounts.AddRange(checking, savings);
        await database.SaveChangesAsync();
        var goal = NewGoal(savings.Id);
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var dueDate = new DateTime(2026, 11, 20);
        var schedule = new RecurringSavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 90m,
            Frequency = SavingsScheduleFrequency.Monthly,
            NextDueDate = dueDate,
            IsActive = true
        };
        database.RecurringSavingsContributions.Add(schedule);
        database.Transactions.Add(new FinancialTransaction
        {
            AccountId = checking.Id,
            RelatedAccountId = savings.Id,
            Type = TransactionType.Transfer,
            Description = "Monthly reserve transfer",
            Amount = 90m,
            CurrencyCode = "USD",
            TransactionDate = dueDate
        });
        await database.SaveChangesAsync();

        var controller = new SavingsGoalsController(database, new SavingsGoalService(), new SavingsGoalProjectionCalculator());
        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var model = Assert.IsType<SavingsGoalsIndexViewModel>(result.Model);

        var suggestion = Assert.Single(model.ScheduleMatchSuggestions);
        Assert.Equal(schedule.Id, suggestion.ScheduleId);
        Assert.Equal("Monthly reserve transfer", suggestion.TransactionDescription);
    }

    [Fact]
    public async Task Index_DoesNotSuggestAmbiguousMatches()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var account = NewAccount("Savings", AccountType.Savings);
        database.Accounts.Add(account);
        await database.SaveChangesAsync();
        var goal = NewGoal(account.Id);
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var date = new DateTime(2026, 11, 20);
        database.SavingsContributions.AddRange(
            new SavingsContribution { SavingsGoalId = goal.Id, Amount = 100m, Type = SavingsContributionType.Contribution, ContributionDate = date },
            new SavingsContribution { SavingsGoalId = goal.Id, Amount = 100m, Type = SavingsContributionType.Contribution, ContributionDate = date });
        database.Transactions.Add(new FinancialTransaction
        {
            AccountId = account.Id,
            Type = TransactionType.Deposit,
            Description = "Savings deposit",
            Amount = 100m,
            CurrencyCode = "USD",
            TransactionDate = date
        });
        await database.SaveChangesAsync();

        var controller = new SavingsGoalsController(database, new SavingsGoalService(), new SavingsGoalProjectionCalculator());
        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var model = Assert.IsType<SavingsGoalsIndexViewModel>(result.Model);

        Assert.Empty(model.MatchingSuggestions);
    }

    [Fact]
    public async Task LinkExistingTransaction_RejectsTransactionAffectingDifferentAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var savings = NewAccount("Savings", AccountType.Savings);
        var checking = NewAccount("Checking", AccountType.Checking);
        database.Accounts.AddRange(savings, checking);
        await database.SaveChangesAsync();
        var goal = NewGoal(savings.Id);
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var date = new DateTime(2026, 11, 20);
        var contribution = new SavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 125m,
            Type = SavingsContributionType.Contribution,
            ContributionDate = date
        };
        var transaction = new FinancialTransaction
        {
            AccountId = checking.Id,
            Type = TransactionType.Deposit,
            Description = "Deposit into checking",
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = date
        };
        database.SavingsContributions.Add(contribution);
        database.Transactions.Add(transaction);
        await database.SaveChangesAsync();

        var controller = new SavingsGoalsController(database, new SavingsGoalService(), new SavingsGoalProjectionCalculator())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        var result = await controller.LinkExistingTransaction(contribution.Id, transaction.Id, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        await database.Entry(contribution).ReloadAsync();
        Assert.Null(contribution.TransactionId);
    }


    [Fact]
    public async Task RecordScheduledContribution_ConfirmsAndLinksMatchingTransfer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var checking = NewAccount("Checking", AccountType.Checking);
        var savings = NewAccount("Savings", AccountType.Savings);
        database.Accounts.AddRange(checking, savings);
        await database.SaveChangesAsync();
        var goal = NewGoal(savings.Id);
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var dueDate = new DateTime(2026, 11, 20);
        var schedule = new RecurringSavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 100m,
            Frequency = SavingsScheduleFrequency.Monthly,
            NextDueDate = dueDate,
            IsActive = true
        };
        var transaction = new FinancialTransaction
        {
            AccountId = checking.Id,
            RelatedAccountId = savings.Id,
            Type = TransactionType.Transfer,
            Description = "Scheduled savings transfer",
            Amount = 100m,
            CurrencyCode = "USD",
            TransactionDate = dueDate
        };
        database.RecurringSavingsContributions.Add(schedule);
        database.Transactions.Add(transaction);
        await database.SaveChangesAsync();

        var controller = new SavingsGoalsController(database, new SavingsGoalService(), new SavingsGoalProjectionCalculator())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        var result = await controller.RecordScheduledContribution(schedule.Id, transaction.Id, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var contribution = await database.SavingsContributions.SingleAsync();
        Assert.Equal(transaction.Id, contribution.TransactionId);
        Assert.Equal(dueDate, contribution.ScheduledDueDate);
        Assert.Equal(dueDate, contribution.ContributionDate);
        await database.Entry(schedule).ReloadAsync();
        Assert.Equal(new DateTime(2026, 12, 20), schedule.NextDueDate);
        Assert.Equal(1, await database.Transactions.CountAsync());
    }

    private static Account NewAccount(string name, AccountType type) => new()
    {
        Name = name,
        Type = type,
        CurrencyCode = "USD",
        OpeningBalance = 0m,
        Status = AccountStatus.Active
    };

    private static SavingsGoal NewGoal(int accountId) => new()
    {
        Name = "Emergency reserve",
        TargetAmount = 5000m,
        CurrencyCode = "USD",
        StartDate = new DateTime(2026, 1, 1),
        Status = SavingsGoalStatus.Active,
        LinkedAccountId = accountId
    };

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
