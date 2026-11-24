using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class TransactionCategoryMappingTests
{
    [Theory]
    [InlineData("  Food   and Dining ", "FOOD AND DINING")]
    [InlineData("salary", "SALARY")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeCategory_ProducesStableLookupKey(string? source, string expected)
    {
        Assert.Equal(expected, StatementImportPreparationService.NormalizeCategory(source));
    }

    [Theory]
    [InlineData(TransactionCategoryType.Income, TransactionType.Income, true)]
    [InlineData(TransactionCategoryType.Income, TransactionType.Deposit, true)]
    [InlineData(TransactionCategoryType.Income, TransactionType.Expense, false)]
    [InlineData(TransactionCategoryType.Expense, TransactionType.Expense, true)]
    [InlineData(TransactionCategoryType.Expense, TransactionType.Withdrawal, true)]
    [InlineData(TransactionCategoryType.Expense, TransactionType.Income, false)]
    [InlineData(TransactionCategoryType.Transfer, TransactionType.Transfer, true)]
    [InlineData(TransactionCategoryType.Transfer, TransactionType.Deposit, false)]
    [InlineData(TransactionCategoryType.Transfer, TransactionType.Withdrawal, false)]
    public void IsCompatible_OnlyAllowsMatchingTransactionTypes(
        TransactionCategoryType categoryType,
        TransactionType transactionType,
        bool expected)
    {
        Assert.Equal(expected,
            StatementImportPreparationService.IsCompatible(categoryType, transactionType));
    }
    [Fact]
    public async Task ApplicationModel_CreatesCategoryAndMappingSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var database = new ApplicationDbContext(options);
        await database.Database.EnsureCreatedAsync();

        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        Assert.Contains("TransactionCategories", tables);
        Assert.Contains("TransactionRules", tables);
        Assert.Contains("TransactionCategoryMappings", tables);
        Assert.Contains("Transactions", tables);
        Assert.Contains("StatementImportRows", tables);
    }

}
