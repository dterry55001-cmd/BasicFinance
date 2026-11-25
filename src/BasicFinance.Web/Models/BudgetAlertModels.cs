namespace BasicFinance.Web.Models;

public sealed class BudgetAlertPreference
{
    // A single global preference row is used until per-user settings are introduced.
    public int Id { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BudgetAlert
{
    public int Id { get; set; }
    public int BudgetPeriodId { get; set; }
    public BudgetPeriod BudgetPeriod { get; set; } = null!;
    public int CategoryId { get; set; }
    public TransactionCategory Category { get; set; } = null!;
    public int ThresholdPercent { get; set; }
    public decimal ActualAmount { get; set; }
    public string Level { get; set; } = "Information";
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
