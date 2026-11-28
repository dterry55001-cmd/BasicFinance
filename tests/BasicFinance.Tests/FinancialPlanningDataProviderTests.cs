using BasicFinance.Web.Data;
using BasicFinance.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BasicFinance.Tests;

public sealed class FinancialPlanningDataProviderTests
{
    [Fact]
    public async Task GetPlanningDataAsync_UsesHistoricalEndOfDayAndKeepsCurrenciesSeparate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var account = new BasicFinance.Web.Models.Account
        {
            Name = "Checking", Type = BasicFinance.Web.Models.AccountType.Checking,
            CurrencyCode = "USD", OpeningBalance = 100m
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        db.Transactions.AddRange(
            new BasicFinance.Web.Models.FinancialTransaction
            {
                AccountId = account.Id, Type = BasicFinance.Web.Models.TransactionType.Income,
                Description = "As-of income", Amount = 50m, CurrencyCode = "USD",
                TransactionDate = new DateTime(2026, 11, 27, 18, 0, 0)
            },
            new BasicFinance.Web.Models.FinancialTransaction
            {
                AccountId = account.Id, Type = BasicFinance.Web.Models.TransactionType.Income,
                Description = "Future income", Amount = 900m, CurrencyCode = "USD",
                TransactionDate = new DateTime(2026, 11, 28)
            });
        await db.SaveChangesAsync();

        var provider = new FinancialPlanningDataProvider(db, new AccountBalanceService(db));
        var result = await provider.GetPlanningDataAsync(new DateOnly(2026, 11, 27), "USD");

        Assert.Single(result.Accounts);
        Assert.Equal(150m, result.Accounts[0].Balance);
        Assert.Empty(result.Debts);
        Assert.Contains(result.Sources, x => x.SourceName == "Accounts" && x.Status == PlanningSourceStatus.Available);
    }

    [Fact]
    public async Task GetPlanningDataAsync_RejectsInvalidReportingCurrency()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        var provider = new FinancialPlanningDataProvider(db, new AccountBalanceService(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetPlanningDataAsync(new DateOnly(2026, 11, 27), "usd"));
    }
}
