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
            "AccountBalanceSnapshots", "InterestRecords", "DebtToIncomeRecords"
        };

        foreach (var table in expectedTables)
        {
            var exists = await database.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {0}", table)
                .SingleAsync();
            Assert.Equal(1, exists);
        }
    }
}
