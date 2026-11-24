using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

public sealed class AccountBalanceService(ApplicationDbContext database)
{
    public async Task<decimal> GetBalanceAsync(int accountId, CancellationToken cancellationToken = default)
    {
        var opening = await database.Accounts.AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => (decimal?)account.OpeningBalance)
            .SingleOrDefaultAsync(cancellationToken) ?? 0m;

        var entries = await database.Transactions.AsNoTracking()
            .Where(transaction => transaction.AccountId == accountId || transaction.RelatedAccountId == accountId)
            .Select(transaction => new
            {
                transaction.AccountId,
                transaction.RelatedAccountId,
                transaction.Type,
                transaction.Amount
            })
            .ToListAsync(cancellationToken);

        return opening + entries.Sum(entry =>
        {
            if (entry.Type == TransactionType.Transfer)
                return entry.AccountId == accountId ? -entry.Amount : entry.Amount;

            return entry.Type is TransactionType.Income or TransactionType.Deposit
                ? entry.Amount
                : -entry.Amount;
        });
    }
}
