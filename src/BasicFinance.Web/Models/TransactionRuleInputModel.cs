using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class TransactionRuleInputModel
{
    public int Id { get; set; }
    [Required, StringLength(100)]
    [Display(Name = "Rule name")]
    public string Name { get; set; } = string.Empty;
    [Required, StringLength(160)]
    [Display(Name = "Description match")]
    public string MatchText { get; set; } = string.Empty;
    [Display(Name = "Match method")]
    public TransactionRuleMatchType MatchType { get; set; } = TransactionRuleMatchType.Contains;
    [Display(Name = "Limit to account")]
    public int? AccountId { get; set; }
    [Display(Name = "Transaction type")]
    public TransactionType? TransactionType { get; set; }
    [Range(1, int.MaxValue)]
    [Display(Name = "Category")]
    public int TransactionCategoryId { get; set; }
    [Range(0, 10000)]
    public int Priority { get; set; } = 100;
}
