using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class CreditScoreImpactInput
{
    [Required]
    [Display(Name = "Revolving account")]
    public int AccountId { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Hypothetical balance reduction")]
    public decimal BalanceReduction { get; set; }

    [Range(typeof(decimal), "-999999999999", "999999999999")]
    [Display(Name = "Hypothetical credit-limit change")]
    public decimal CreditLimitChange { get; set; }
}

public sealed record CreditScoreImpactAccount(int AccountId, string Name, decimal Balance, decimal CreditLimit, decimal UtilizationPercent, decimal ProjectedBalance, decimal ProjectedLimit, decimal ProjectedUtilizationPercent);

public sealed class CreditScoreImpactResult
{
    public decimal CurrentTotalBalance { get; set; }
    public decimal CurrentTotalLimit { get; set; }
    public decimal CurrentUtilizationPercent { get; set; }
    public decimal ProjectedTotalBalance { get; set; }
    public decimal ProjectedTotalLimit { get; set; }
    public decimal ProjectedUtilizationPercent { get; set; }
    public decimal UtilizationChangePercentagePoints { get; set; }
    public int HardInquiriesLastTwelveMonths { get; set; }
    public int SoftInquiriesLastTwelveMonths { get; set; }
    public IReadOnlyList<CreditScoreImpactAccount> Accounts { get; set; } = [];
    public string UtilizationIndicator => ProjectedUtilizationPercent < 10m ? "Below 10%" :
        ProjectedUtilizationPercent < 30m ? "Below 30%" :
        ProjectedUtilizationPercent < 50m ? "30–49.9%" :
        ProjectedUtilizationPercent < 75m ? "50–74.9%" :
        ProjectedUtilizationPercent < 90m ? "75–89.9%" : "90% or higher";
}
