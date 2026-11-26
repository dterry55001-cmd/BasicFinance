namespace BasicFinance.Web.Models;

public enum CashFlowAlertType
{
    LowBalance,
    ProjectedShortage
}

public sealed class CashFlowAlert
{
    public int Id { get; set; }
    public CashFlowAlertType Type { get; set; }
    public DateTime ForecastDate { get; set; }
    public decimal ProjectedBalance { get; set; }
    public decimal ThresholdAmount { get; set; }
    public string AlertKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
