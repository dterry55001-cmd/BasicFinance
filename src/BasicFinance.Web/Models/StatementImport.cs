using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum StatementImportStatus { Preview, Completed, CompletedWithErrors }
public enum StatementImportProfile
{
    Unknown, UbsBrokerage, LifeSightRetirement, AtomicBanking,
    RobinhoodBrokerage, RobinhoodBank, ChaseBanking, OfxBanking, QifBanking
}

public sealed class StatementImport
{
    public int Id { get; set; }
    [Required, StringLength(255)]
    public string FileName { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public StatementImportStatus Status { get; set; } = StatementImportStatus.Preview;
    public StatementImportProfile Profile { get; set; } = StatementImportProfile.Unknown;
    [StringLength(64)] public string? AccountKey { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    [StringLength(4)] public string? ExternalAccountNumberLast4 { get; set; }
    [StringLength(100)] public string? FriendlyAccountName { get; set; }
    public ICollection<StatementImportRow> Rows { get; set; } = new List<StatementImportRow>();
}

public sealed class StatementImportRow
{
    public int Id { get; set; }
    public int StatementImportId { get; set; }
    public StatementImport StatementImport { get; set; } = null!;
    public int RowNumber { get; set; }
    public DateTime? TransactionDate { get; set; }
    public DateTime? PostingDate { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ActivityDate { get; set; }
    public DateTime? ProcessDate { get; set; }
    public DateTime? SettlementDate { get; set; }
    [StringLength(160)]
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? SourceSignedAmount { get; set; }
    public TransactionType Type { get; set; }
    [StringLength(80)]
    public string? Category { get; set; }
    public int? CategoryId { get; set; }
    public TransactionCategory? TransactionCategory { get; set; }
    public bool CategorySuggestedByRule { get; set; }
    [StringLength(100)]
    public string? Reference { get; set; }
    [StringLength(100)] public string? Activity { get; set; }
    [StringLength(100)] public string? SourceType { get; set; }
    [StringLength(100)] public string? SourceSubType { get; set; }
    [StringLength(200)] public string? ExtendedDescription { get; set; }
    [StringLength(100)] public string? SourceStatus { get; set; }
    [StringLength(100)] public string? SourceCategory { get; set; }
    [StringLength(120)] public string? SourceReference { get; set; }
    [StringLength(160)] public string? Instrument { get; set; }
    [StringLength(200)] public string? Memo { get; set; }
    [StringLength(80)] public string? CheckNumber { get; set; }
    [StringLength(120)] public string? ReferenceNumber { get; set; }
    [StringLength(120)] public string? TransactionCode { get; set; }
    public decimal? RunningBalance { get; set; }
    [StringLength(64)] public string? DuplicateFingerprint { get; set; }
    public bool RequiresReview { get; set; }
    public string SourceFieldsJson { get; set; } = "{}";
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
    [StringLength(500)]
    public string? ValidationError { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsImported { get; set; }
}
