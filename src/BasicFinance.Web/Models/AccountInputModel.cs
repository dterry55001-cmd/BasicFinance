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

    [Range(typeof(decimal), "0", "100")]
    [Display(Name = "Interest rate / APY (%)")]
    public decimal? InterestRatePercent { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Credit limit")]
    public decimal? CreditLimit { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Minimum payment")]
    public decimal? MinimumPayment { get; set; }

    [Range(1, 31)]
    [Display(Name = "Payment due day of month")]
    public int? PaymentDueDay { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Original loan amount")]
    public decimal? OriginalLoanAmount { get; set; }

    [Range(1, 600)]
    [Display(Name = "Loan term (months)")]
    public int? LoanTermMonths { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Maturity date")]
    public DateTime? MaturityDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Employer")]
    public string? EmployerName { get; set; }

    [Range(typeof(decimal), "0", "100")]
    [Display(Name = "Employer match (%)")]
    public decimal? EmployerMatchPercent { get; set; }

    [StringLength(40)]
    [Display(Name = "Tax treatment")]
    public string? TaxTreatment { get; set; }

    [StringLength(100)]
    [Display(Name = "Brokerage registration")]
    public string? BrokerageRegistrationType { get; set; }

    [StringLength(1000)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

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
