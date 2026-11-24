using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class AccountInputModel
{
    [Required, StringLength(100)]
    [Display(Name = "Account name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Account type")]
    public AccountType Type { get; set; }

    [StringLength(100)]
    public string? Institution { get; set; }

    [StringLength(4, MinimumLength = 4)]
    [RegularExpression(@"^[0-9]{4}$", ErrorMessage = "Enter only the last four digits.")]
    [Display(Name = "Account number (last 4 digits only)")]
    public string? AccountNumberLast4 { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Use a three-letter currency code, such as USD.")]
    [Display(Name = "Currency code")]
    public string CurrencyCode { get; set; } = "USD";

    [Range(typeof(decimal), "-999999999999", "999999999999")]
    [DataType(DataType.Currency)]
    [Display(Name = "Opening balance")]
    public decimal OpeningBalance { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date opened")]
    public DateTime? OpenedOn { get; set; }

    [Required]
    public AccountStatus Status { get; set; } = AccountStatus.Active;
}
