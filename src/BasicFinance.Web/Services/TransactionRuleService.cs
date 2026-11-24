using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

/// <summary>Evaluates user rules and returns a category suggestion without changing ledger data.</summary>
public sealed class TransactionRuleService(ApplicationDbContext database)
{
    public async Task<TransactionCategory?> SuggestAsync(
        string? description, int accountId, TransactionType transactionType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var rules = await database.TransactionRules.AsNoTracking()
            .Include(rule => rule.TransactionCategory)
            .Where(rule => rule.IsActive
                && (rule.AccountId == null || rule.AccountId == accountId)
                && (rule.TransactionType == null || rule.TransactionType == transactionType)
                && rule.TransactionCategory.IsActive)
            .OrderBy(rule => rule.Priority).ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

        var candidate = description.Trim();
        foreach (var rule in rules)
        {
            var matched = rule.MatchType switch
            {
                TransactionRuleMatchType.Exact => string.Equals(candidate, rule.MatchText.Trim(), StringComparison.OrdinalIgnoreCase),
                TransactionRuleMatchType.StartsWith => candidate.StartsWith(rule.MatchText.Trim(), StringComparison.OrdinalIgnoreCase),
                _ => candidate.Contains(rule.MatchText.Trim(), StringComparison.OrdinalIgnoreCase)
            };
            if (!matched) continue;

            var compatible = rule.TransactionCategory.Type switch
            {
                TransactionCategoryType.Income => transactionType is TransactionType.Income or TransactionType.Deposit,
                TransactionCategoryType.Expense => transactionType is TransactionType.Expense or TransactionType.Withdrawal,
                TransactionCategoryType.Transfer => transactionType == TransactionType.Transfer,
                _ => false
            };
            if (compatible) return rule.TransactionCategory;
        }
        return null;
    }
}
