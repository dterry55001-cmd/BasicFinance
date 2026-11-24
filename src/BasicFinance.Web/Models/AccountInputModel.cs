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

    [Display(Name = "Retirement account type")]
    public RetirementAccountType? RetirementType { get; set; }

    [Display(Name = "Debt account type")]
    public DebtAccountType? DebtType { get; set; }

    [StringLength(100)]
    [Display(Name = "Institution / provider")]
    public string? Institution { get; set; }

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
