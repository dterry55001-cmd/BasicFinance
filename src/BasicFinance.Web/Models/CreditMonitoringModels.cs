using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class CreditLimitHistory
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public decimal? PreviousLimit { get; set; }
    public decimal NewLimit { get; set; }
    [DataType(DataType.Date)]
    public DateTime EffectiveDate { get; set; } = DateTime.Today;
    [StringLength(30)]
    public string Source { get; set; } = "Manual";
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ReportedBalanceSnapshot
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    [DataType(DataType.Date)]
    public DateTime ReportedDate { get; set; } = DateTime.Today;
    public decimal ReportedBalance { get; set; }
    public decimal? ReportedCreditLimit { get; set; }
    [StringLength(40)]
    public string Source { get; set; } = "Manual";
    [DataType(DataType.Date)]
    public DateTime? ReceivedDate { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public enum CreditInquiryType { Hard, Soft }

public sealed class CreditInquiry
{
    public int Id { get; set; }
    [DataType(DataType.Date)]
    public DateTime InquiryDate { get; set; } = DateTime.Today;
    [Required, StringLength(60)]
    public string Bureau { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string Lender { get; set; } = string.Empty;
    public CreditInquiryType Type { get; set; } = CreditInquiryType.Hard;
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
