using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class PaymentAllocationInputModel
{
    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$")]
    [Display(Name = "Currency")]
    public string CurrencyCode { get; set; } = "USD";

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Additional monthly amount available")]
    public decimal ExtraMonthlyPayment { get; set; }

    public List<PaymentAllocationDebtRow> Debts { get; set; } = [];
    public DebtPayoffPlan? CurrentPlan { get; set; }
    public DebtPayoffPlan? SnowballPlan { get; set; }
    public DebtPayoffPlan? AvalanchePlan { get; set; }
    public decimal? InterestSavingsVsCurrent { get; set; }
    public int? MonthsSavedVsCurrent { get; set; }
    public string? Notice { get; set; }
    public List<string> MissingInputs { get; set; } = [];
}

public sealed class PaymentAllocationDebtRow
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal AnnualRatePercent { get; set; }
    public decimal MinimumPayment { get; set; }
    public int AvalanchePriority { get; set; }
    public int SnowballPriority { get; set; }
    public DateTime? AvalanchePayoffDate { get; set; }
    public DateTime? SnowballPayoffDate { get; set; }
}
