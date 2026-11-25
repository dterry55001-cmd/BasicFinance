using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BasicFinance.Tests;

public sealed class DebtPaymentLedgerTests
{
    [Fact]
    public async Task PrincipalTransferReducesDebtAndFundingBalance_InterestExpenseOnlyReducesFunding()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.OpenConnectionAsync();
        await database.Database.EnsureCreatedAsync();

        var funding = new Account
        {
            Name = "Checking", Type = AccountType.Checking, CurrencyCode = "USD",
            OpeningBalance = 2000m
        };
        var debt = new Account
        {
            Name = "Auto loan", Type = AccountType.Debt, CurrencyCode = "USD",
            OpeningBalance = 1000m
        };
        database.Accounts.AddRange(funding, debt);
        await database.SaveChangesAsync();

        database.Transactions.AddRange(
            new FinancialTransaction
            {
                AccountId = funding.Id, RelatedAccountId = debt.Id,
                Type = TransactionType.Transfer, Description = "Principal payment",
                Amount = 100m, CurrencyCode = "USD", TransactionDate = new DateTime(2026, 1, 15)
            },
            new FinancialTransaction
            {
                AccountId = funding.Id, Type = TransactionType.Expense,
                Description = "Interest and fees", Amount = 30m,
                CurrencyCode = "USD", TransactionDate = new DateTime(2026, 1, 15)
            });
        await database.SaveChangesAsync();

        var balances = new AccountBalanceService(database);
        Assert.Equal(1870m, await balances.GetBalanceAsync(funding.Id));
        Assert.Equal(900m, await balances.GetBalanceAsync(debt.Id));
    }
}
