using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum InterestRecordType
{
    Earned,
    Charged
}

public sealed class InterestRecord
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public int? TransactionId { get; set; }
    public FinancialTransaction? Transaction { get; set; }
    [DataType(DataType.Date)]
    public DateTime InterestDate { get; set; } = DateTime.Today;
    public InterestRecordType Type { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }
    [StringLength(160)]
    public string Description { get; set; } = "Interest";
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
