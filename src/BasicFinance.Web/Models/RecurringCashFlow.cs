using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum CashFlowFrequency
{
    Weekly,
    Biweekly,
    Monthly,
    Quarterly,
    Yearly
}

public sealed class RecurringCashFlow
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    public TransactionType Type { get; set; }

    [Required, StringLength(160)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "USD";

    public CashFlowFrequency Frequency { get; set; }
    [DataType(DataType.Date)]
    public DateTime NextDueDate { get; set; } = DateTime.Today;
    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
