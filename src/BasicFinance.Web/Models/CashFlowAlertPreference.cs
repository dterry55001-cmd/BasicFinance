using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class CashFlowAlertPreference
{
    public int Id { get; set; } = 1;
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal LowBalanceThreshold { get; set; } = 250m;
    public bool AlertsEnabled { get; set; } = true;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
