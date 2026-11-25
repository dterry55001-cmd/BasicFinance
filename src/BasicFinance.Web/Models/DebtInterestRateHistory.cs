using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtInterestRateHistory
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    [Range(typeof(decimal), "0", "100")]
    public decimal AnnualRatePercent { get; set; }
    [DataType(DataType.Date)]
    public DateTime EffectiveDate { get; set; } = DateTime.Today;
    [StringLength(30)]
    public string RateType { get; set; } = "Variable";
    [DataType(DataType.Date)]
    public DateTime? PromotionalEndDate { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
}
