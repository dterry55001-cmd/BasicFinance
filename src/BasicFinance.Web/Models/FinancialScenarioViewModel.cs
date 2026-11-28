using BasicFinance.Web.Models;
using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class FinancialScenarioViewModel
{
    public decimal StartingDebtBalance { get; set; }
    public decimal AnnualDebtRatePercent { get; set; }
    public decimal MonthlyDebtPayment { get; set; }
    public decimal StartingSavingsBalance { get; set; }
    public decimal MonthlySavingsContribution { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }

    [Range(-1000000, 1000000)]
    public decimal MonthlyIncomeChange { get; set; }
    [Range(-1000000, 1000000)]
    public decimal MonthlyExpenseChange { get; set; }
    [Range(-100, 100)]
    public decimal AnnualDebtRateChange { get; set; }
    [Range(-1000000, 1000000)]
    public decimal MonthlyDebtPaymentChange { get; set; }
    [Range(-1000000, 1000000)]
    public decimal MonthlySavingsContributionChange { get; set; }
    [Range(1, 120)]
    public int ProjectionMonths { get; set; } = 24;
    public FinancialScenarioComparison? Comparison { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = [];
}
