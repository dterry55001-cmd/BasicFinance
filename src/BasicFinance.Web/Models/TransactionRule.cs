using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum TransactionRuleMatchType { Contains, Exact, StartsWith }

/// <summary>A user-defined rule that suggests a category; it never posts or edits transactions.</summary>
public sealed class TransactionRule
{
    public int Id { get; set; }
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, StringLength(160)]
    public string MatchText { get; set; } = string.Empty;
    public TransactionRuleMatchType MatchType { get; set; } = TransactionRuleMatchType.Contains;
    public int? AccountId { get; set; }
    public Account? Account { get; set; }
    public TransactionType? TransactionType { get; set; }
    public int TransactionCategoryId { get; set; }
    public TransactionCategory TransactionCategory { get; set; } = null!;
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
