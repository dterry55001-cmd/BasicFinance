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

public sealed class SavingsReconciliationControllerTests
{
    [Fact]
    public async Task Run_RecognizesIncomingTransferAndAllowsExplicitMatch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var sourceAccount = new Account
        {
            Name = "Checking",
            Type = AccountType.Checking,
            CurrencyCode = "USD",
            OpeningBalance = 2000m,
            Status = AccountStatus.Active
        };
        var savingsAccount = new Account
        {
            Name = "Emergency savings",
            Type = AccountType.Savings,
            CurrencyCode = "USD",
            OpeningBalance = 1000m,
            Status = AccountStatus.Active
        };
        database.Accounts.AddRange(sourceAccount, savingsAccount);
        await database.SaveChangesAsync();

        var goal = new SavingsGoal
        {
            Name = "Emergency reserve",
            TargetAmount = 5000m,
            CurrencyCode = "USD",
            StartDate = new DateTime(2026, 1, 1),
            Status = SavingsGoalStatus.Active,
            LinkedAccountId = savingsAccount.Id
        };
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var transactionDate = new DateTime(2026, 11, 5);
        var transaction = new FinancialTransaction
        {
            AccountId = sourceAccount.Id,
            RelatedAccountId = savingsAccount.Id,
            Type = TransactionType.Transfer,
            Description = "Transfer to emergency savings",
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = transactionDate
        };
        var contribution = new SavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = 125m,
            Type = SavingsContributionType.Contribution,
            ContributionDate = transactionDate
        };
        database.Transactions.Add(transaction);
        database.SavingsContributions.Add(contribution);
        await database.SaveChangesAsync();

        var controller = new SavingsReconciliationController(
            database, new AccountBalanceService(database), new SavingsGoalService());
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new InMemoryTempDataProvider());

        var runResult = await controller.Run(goal.Id, new DateTime(2026, 11, 1), DateTime.Today,
            1125m, CancellationToken.None);
        var redirect = Assert.IsType<RedirectToActionResult>(runResult);
        Assert.Equal(nameof(SavingsReconciliationController.Details), redirect.ActionName);

        var session = await database.SavingsReconciliationSessions
            .Include(x => x.Items).SingleAsync();
        Assert.Equal(1125m, session.CalculatedAccountBalance);
        Assert.Equal(0m, session.Difference);
        var transactionItem = Assert.Single(session.Items, x => x.TransactionId == transaction.Id);
        Assert.Equal(SavingsReconciliationItemStatus.UnmatchedTransaction, transactionItem.Status);

        var matchResult = await controller.Match(session.Id, transaction.Id, contribution.Id, CancellationToken.None);
        Assert.IsType<RedirectToActionResult>(matchResult);

        await database.Entry(contribution).ReloadAsync();
        Assert.Equal(transaction.Id, contribution.TransactionId);
        await database.Entry(transactionItem).ReloadAsync();
        Assert.Equal(SavingsReconciliationItemStatus.Matched, transactionItem.Status);
        await database.Entry(session).ReloadAsync();
        Assert.Equal(1, session.MatchedCount);
        Assert.Equal(0, session.UnmatchedContributionCount);
    }

    [Fact]
    public async Task Run_RejectsInvalidPeriodAndUnlinkedGoal()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var goal = new SavingsGoal
        {
            Name = "Vacation",
            TargetAmount = 1000m,
            CurrencyCode = "USD",
            StartDate = new DateTime(2026, 1, 1),
            Status = SavingsGoalStatus.Active
        };
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var controller = new SavingsReconciliationController(
            database, new AccountBalanceService(database), new SavingsGoalService());
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new InMemoryTempDataProvider());

        var result = await controller.Run(goal.Id, DateTime.Today, DateTime.Today.AddDays(1), 0m, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(await database.SavingsReconciliationSessions.ToListAsync());
    }
}


internal sealed class InMemoryTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
