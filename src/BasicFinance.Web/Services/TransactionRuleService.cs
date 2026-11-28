using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BasicFinance.Web.Services;

/// <summary>
/// Evaluates user-defined categorization rules and returns a suggestion without
/// changing ledger data. Matching supports both the original description and a
/// normalized merchant-friendly representation.
/// </summary>
public sealed class TransactionRuleService(ApplicationDbContext database)
{
    private static readonly string[] TransactionPrefixes =
    [
        "POINT OF SALE", "POS DEBIT", "POS PURCHASE", "DEBIT CARD PURCHASE",
        "DEBIT CARD", "CARD PURCHASE", "PURCHASE", "CHECKCARD", "RECURRING"
    ];

    public async Task<TransactionCategory?> SuggestAsync(
        string? description, int accountId, TransactionType transactionType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var candidate = description.Trim();
        var normalizedCandidate = NormalizeMerchantDescription(candidate);
        if (normalizedCandidate.Length == 0) return null;

        var rules = await database.TransactionRules.AsNoTracking()
            .Include(rule => rule.TransactionCategory)
            .Where(rule => rule.IsActive
                && (rule.AccountId == null || rule.AccountId == accountId)
                && (rule.TransactionType == null || rule.TransactionType == transactionType)
                && rule.TransactionCategory.IsActive)
            .OrderBy(rule => rule.Priority).ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            var rawMatchText = rule.MatchText.Trim();
            var normalizedMatchText = NormalizeMerchantDescription(rawMatchText);
            var matched = Matches(candidate, rawMatchText, rule.MatchType)
                || (normalizedMatchText.Length > 0
                    && Matches(normalizedCandidate, normalizedMatchText, rule.MatchType));
            if (!matched) continue;

            if (IsCompatible(rule.TransactionCategory.Type, transactionType))
                return rule.TransactionCategory;
        }
        return null;
    }

    /// <summary>
    /// Removes common statement formatting noise while preserving merchant words.
    /// This deliberately does not remove meaningful words or numeric merchant names.
    /// </summary>
    public static string NormalizeMerchantDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;

        var value = description.Trim().ToUpperInvariant();
        foreach (var prefix in TransactionPrefixes)
        {
            if (value.StartsWith(prefix + " ", StringComparison.Ordinal))
            {
                value = value[(prefix.Length + 1)..].TrimStart();
                break;
            }
        }

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = true;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool Matches(string candidate, string matchText, TransactionRuleMatchType matchType) =>
        matchType switch
        {
            TransactionRuleMatchType.Exact =>
                string.Equals(candidate, matchText, StringComparison.OrdinalIgnoreCase),
            TransactionRuleMatchType.StartsWith =>
                candidate.StartsWith(matchText, StringComparison.OrdinalIgnoreCase),
            _ => candidate.Contains(matchText, StringComparison.OrdinalIgnoreCase)
        };

    private static bool IsCompatible(TransactionCategoryType categoryType, TransactionType transactionType) =>
        categoryType switch
        {
            TransactionCategoryType.Income => transactionType is TransactionType.Income or TransactionType.Deposit,
            TransactionCategoryType.Expense => transactionType is TransactionType.Expense or TransactionType.Withdrawal,
            TransactionCategoryType.Transfer => transactionType == TransactionType.Transfer,
            _ => false
        };
}
