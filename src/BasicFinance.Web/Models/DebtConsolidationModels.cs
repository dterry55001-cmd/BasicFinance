using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtConsolidationInput
{
    [Required]
    public List<int> AccountIds { get; set; } = [];
    [Range(typeof(decimal), "0", "100")]
    [Display(Name = "Consolidation APR (%)")]
    public decimal AnnualRatePercent { get; set; }
    [Range(1, 600)]
    [Display(Name = "Repayment term (months)")]
    public int TermMonths { get; set; } = 60;
    [Range(typeof(decimal), "0", "100")]
    [Display(Name = "Origination fee (%)")]
    public decimal OriginationFeePercent { get; set; }
    [DataType(DataType.Date)]
    public DateTime FirstPaymentDate { get; set; } = DateTime.Today.AddMonths(1);
}

public sealed class DebtConsolidationDebt
{
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public decimal Balance { get; set; }
    public decimal AnnualRatePercent { get; set; }
    public decimal MinimumPayment { get; set; }
}

public sealed class DebtConsolidationResult
{
    public decimal Principal { get; set; }
    public decimal OriginationFee { get; set; }
    public decimal FinancedAmount { get; set; }
    public decimal MonthlyPayment { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal TotalCost { get; set; }
    public decimal CurrentMonthlyMinimums { get; set; }
    public decimal MonthlyCashFlowDifference => CurrentMonthlyMinimums - MonthlyPayment;
    public decimal CurrentWeightedApr { get; set; }
    public decimal ConsolidationApr { get; set; }
    public int TermMonths { get; set; }
    public DateTime PayoffDate { get; set; }
    public IReadOnlyList<DebtConsolidationDebt> Debts { get; set; } = [];
}
