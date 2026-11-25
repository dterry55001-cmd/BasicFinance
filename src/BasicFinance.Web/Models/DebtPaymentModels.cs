using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum DebtPaymentStatus
{
    Scheduled,
    Paid,
    Skipped
}

public sealed class DebtPaymentRecord
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal ExpectedAmount { get; set; }
    public DebtPaymentStatus Status { get; set; } = DebtPaymentStatus.Scheduled;
    [DataType(DataType.Date)]
    public DateTime? PaidDate { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? PrincipalPaid { get; set; }
    public decimal? InterestPaid { get; set; }
    public decimal? FeesPaid { get; set; }
    public int? SourceAccountId { get; set; }
    public Account? SourceAccount { get; set; }
    public int? TransactionId { get; set; }
    public FinancialTransaction? Transaction { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsPastDue(DateTime today) => Status == DebtPaymentStatus.Scheduled && DueDate.Date < today.Date;
}

public sealed class RecordDebtPaymentInput
{
    public int PaymentRecordId { get; set; }
    [Required]
    public int SourceAccountId { get; set; }
    [DataType(DataType.Date)]
    public DateTime PaidDate { get; set; } = DateTime.Today;
    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal ActualAmount { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal PrincipalPaid { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal InterestPaid { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal FeesPaid { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class ScheduleDebtPaymentInput
{
    public int AccountId { get; set; }
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; } = DateTime.Today.AddMonths(1);
    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal ExpectedAmount { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class DebtProgressRow
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public decimal OriginalBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal PrincipalReduced { get; set; }
    public decimal PercentPaid { get; set; }
    public decimal PrincipalRecorded { get; set; }
}
