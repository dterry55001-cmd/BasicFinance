using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum TransactionType
{
    Income,
    Expense,
    Deposit,
    Withdrawal,
    Transfer
}

public sealed class FinancialTransaction
{
    public int Id { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int? RelatedAccountId { get; set; }
    public Account? RelatedAccount { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    [Required, StringLength(160)]
    public string Description { get; set; } = string.Empty;

    [StringLength(80)]
    public string? Category { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "USD";

    [DataType(DataType.Date)]
    public DateTime TransactionDate { get; set; } = DateTime.Today;

    [StringLength(100)]
    public string? Reference { get; set; }
    [StringLength(100)] public string? Activity { get; set; }
    [StringLength(40)] public string? Symbol { get; set; }
    [StringLength(20)] public string? Cusip { get; set; }
    [StringLength(40)] public string? SecurityType { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    [StringLength(4)] public string? ExternalAccountNumberLast4 { get; set; }
    [StringLength(100)] public string? FriendlyAccountName { get; set; }

    [StringLength(120)]
    public string? Source { get; set; }

    [StringLength(160)]
    public string? FundName { get; set; }

    public decimal? UnitCount { get; set; }
    public decimal? UnitValue { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
