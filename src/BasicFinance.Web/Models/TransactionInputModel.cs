using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class TransactionInputModel : IValidatableObject
{
    [Required]
    [Display(Name = "Account")]
    public int AccountId { get; set; }

    [Display(Name = "Transfer to account")]
    public int? RelatedAccountId { get; set; }

    [Required]
    [Display(Name = "Transaction type")]
    public TransactionType Type { get; set; }

    [Required, StringLength(160)]
    public string Description { get; set; } = string.Empty;

    [StringLength(80)]
    public string? Category { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Use a three-letter currency code.")]
    [Display(Name = "Currency code")]
    public string CurrencyCode { get; set; } = "USD";

    [DataType(DataType.Date)]
    [Display(Name = "Transaction date")]
    public DateTime TransactionDate { get; set; } = DateTime.Today;

    [StringLength(100)]
    public string? Reference { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == TransactionType.Transfer)
        {
            if (RelatedAccountId is null)
                yield return new ValidationResult("Choose the destination account for this transfer.", [nameof(RelatedAccountId)]);
            else if (RelatedAccountId == AccountId)
                yield return new ValidationResult("A transfer must use two different accounts.", [nameof(RelatedAccountId)]);
        }
        else if (RelatedAccountId is not null)
        {
            yield return new ValidationResult("A destination account is only used for transfers.", [nameof(RelatedAccountId)]);
        }
    }
}
