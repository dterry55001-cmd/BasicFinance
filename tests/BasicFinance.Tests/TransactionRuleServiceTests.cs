using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BasicFinance.Tests;

public sealed class TransactionRuleServiceTests
{
    [Fact]
    public async Task SuggestAsync_MatchesDescriptionWithoutPostingTransaction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var account = new Account { Name = "Daily Checking", Type = AccountType.Checking, CurrencyCode = "USD" };
        var category = new TransactionCategory { Name = "Fuel", Type = TransactionCategoryType.Expense };
        database.AddRange(account, category);
        await database.SaveChangesAsync();
        database.TransactionRules.Add(new TransactionRule
        {
            Name = "Fuel merchants",
            MatchText = "SHELL",
            MatchType = TransactionRuleMatchType.Contains,
            TransactionCategoryId = category.Id,
            TransactionType = TransactionType.Expense,
            Priority = 10
        });
        await database.SaveChangesAsync();

        var service = new TransactionRuleService(database);
        var suggestion = await service.SuggestAsync("SHELL OIL 1234", account.Id, TransactionType.Expense);

        Assert.NotNull(suggestion);
        Assert.Equal(category.Id, suggestion.Id);
        Assert.Empty(await database.Transactions.ToListAsync());
    }

    [Fact]
    public async Task SuggestAsync_DoesNotReturnCategoryForIncompatibleTransactionType()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var category = new TransactionCategory { Name = "Salary", Type = TransactionCategoryType.Income };
        database.TransactionCategories.Add(category);
        await database.SaveChangesAsync();
        database.TransactionRules.Add(new TransactionRule
        {
            Name = "Payroll",
            MatchText = "PAYROLL",
            MatchType = TransactionRuleMatchType.Contains,
            TransactionCategoryId = category.Id,
            TransactionType = TransactionType.Income
        });
        await database.SaveChangesAsync();

        var service = new TransactionRuleService(database);
        var suggestion = await service.SuggestAsync("PAYROLL DEPOSIT", 1, TransactionType.Expense);

        Assert.Null(suggestion);
    }

    [Fact]
    public async Task SuggestAsync_PrefersLowerPriorityNumber()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var first = new TransactionCategory { Name = "Fuel", Type = TransactionCategoryType.Expense };
        var second = new TransactionCategory { Name = "Auto", Type = TransactionCategoryType.Expense };
        database.TransactionCategories.AddRange(first, second);
        await database.SaveChangesAsync();
        database.TransactionRules.AddRange(
            new TransactionRule { Name = "General gas", MatchText = "GAS", TransactionCategoryId = second.Id, Priority = 100 },
            new TransactionRule { Name = "Specific shell", MatchText = "SHELL", TransactionCategoryId = first.Id, Priority = 10 });
        await database.SaveChangesAsync();

        var service = new TransactionRuleService(database);
        var suggestion = await service.SuggestAsync("SHELL GAS STATION", 1, TransactionType.Expense);

        Assert.NotNull(suggestion);
        Assert.Equal(first.Id, suggestion.Id);
    }
}
