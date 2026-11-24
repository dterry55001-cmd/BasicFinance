using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum AccountType
{
    Checking,
    Savings,
    Cash,
    CreditCard,
    Brokerage,
    Retirement
}

public enum RetirementAccountType
{
    Employer401k,
    Employer401a,
    Employer403b,
    Government457b,
    ThriftSavingsPlan,
    TraditionalIra,
    RothIra,
    RolloverIra,
    InheritedIra,
    SepIra,
    SimpleIra,
    Solo401k,
    DefinedBenefitPension,
    CashBalancePension,
    Annuity,
    OtherRetirement
}

public enum AccountStatus
{
    Active,
    Inactive,
    Archived
}

public sealed class Account
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public AccountType Type { get; set; }

    public RetirementAccountType? RetirementType { get; set; }

    [StringLength(100)]
    public string? Institution { get; set; }

    [StringLength(4, MinimumLength = 4)]
    [RegularExpression(@"^[0-9]{4}$", ErrorMessage = "Enter only the last four digits.")]
    public string? AccountNumberLast4 { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Use a three-letter currency code, such as USD.")]
    public string CurrencyCode { get; set; } = "USD";

    [Range(typeof(decimal), "-999999999999", "999999999999")]
    public decimal OpeningBalance { get; set; }

    [DataType(DataType.Date)]
    public DateTime? OpenedOn { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
