using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

public sealed class AccountBalanceService(ApplicationDbContext database)
{
    public Task<decimal> GetBalanceAsync(int accountId, CancellationToken cancellationToken = default) =>
        GetBalanceAtAsync(accountId, DateTime.MaxValue, cancellationToken);

    public async Task<decimal> GetBalanceAtAsync(int accountId, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        var account = await database.Accounts.AsNoTracking()
            .Where(item => item.Id == accountId)
            .Select(item => new { item.OpeningBalance, item.Type })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null) return 0m;

        var entries = await database.Transactions.AsNoTracking()
            .Where(transaction => (transaction.AccountId == accountId || transaction.RelatedAccountId == accountId)
                && transaction.TransactionDate <= asOfDate)
            .Select(transaction => new
            {
                transaction.AccountId,
                transaction.Type,
                transaction.Amount
            })
            .ToListAsync(cancellationToken);

        return account.OpeningBalance + entries.Sum(entry =>
        {
            if (entry.Type == TransactionType.Transfer)
            {
                if (account.Type is AccountType.Debt or AccountType.CreditCard)
                    return entry.AccountId == accountId ? entry.Amount : -entry.Amount;
                return entry.AccountId == accountId ? -entry.Amount : entry.Amount;
            }

            if (account.Type is AccountType.Debt or AccountType.CreditCard)
                return entry.Type is TransactionType.Expense or TransactionType.Withdrawal
                    ? entry.Amount
                    : -entry.Amount;

            return entry.Type is TransactionType.Income or TransactionType.Deposit
                ? entry.Amount
                : -entry.Amount;
        });
    }
}
