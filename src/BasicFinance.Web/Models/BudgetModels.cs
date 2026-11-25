using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class BudgetPeriod
{
    public int Id { get; set; }

    [Required, StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Range(2000, 2200)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "USD";

    public ICollection<BudgetLine> Lines { get; set; } = new List<BudgetLine>();

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BudgetLine
{
    public int Id { get; set; }

    public int BudgetPeriodId { get; set; }
    public BudgetPeriod BudgetPeriod { get; set; } = null!;

    public int CategoryId { get; set; }
    public TransactionCategory Category { get; set; } = null!;

    [Range(typeof(decimal), "0.00", "999999999999")]
    public decimal PlannedAmount { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
