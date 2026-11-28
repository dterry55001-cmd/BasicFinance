using BasicFinance.Web.Services;

namespace BasicFinance.Web.Models;

public sealed record FinancialPlanningCurrencySummary(
    string CurrencyCode,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal NetWorth,
    decimal AvailableCash,
    decimal RetirementBalances,
    int AssetAccountCount,
    int LiabilityAccountCount);

public sealed class FinancialPlanningDashboardViewModel
{
    public DateOnly AsOfDate { get; init; }
    public string ReportingCurrency { get; init; } = "USD";
    public IReadOnlyList<FinancialPlanningCurrencySummary> CurrencySummaries { get; init; } = [];
    public IReadOnlyList<PlanningSourceMetadata> Sources { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public int BudgetLineCount { get; init; }
    public decimal PlannedBudgetTotal { get; init; }
    public int SavingsGoalCount { get; init; }
    public decimal SavingsGoalProgressTotal { get; init; }
    public decimal SavingsGoalTargetTotal { get; init; }
    public int DebtAccountsMissingDetails { get; init; }
}
