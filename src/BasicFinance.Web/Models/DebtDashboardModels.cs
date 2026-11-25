namespace BasicFinance.Web.Models;

public sealed class DebtDashboardViewModel
{
    public List<DebtDashboardCurrencySummary> CurrencySummaries { get; set; } = [];
    public List<DebtBalanceTrendPoint> BalanceTrends { get; set; } = [];
    public List<DebtStrategyComparison> StrategyComparisons { get; set; } = [];
    public List<DebtDashboardMilestone> RecentMilestones { get; set; } = [];
    public int ActiveDebtAccountCount { get; set; }
    public int PaidOffAccountCount { get; set; }
}

public sealed record DebtDashboardCurrencySummary(
    string CurrencyCode,
    decimal CurrentBalance,
    decimal OpeningBalance,
    decimal NetReduction,
    int AccountCount,
    decimal MonthlyMinimums);

public sealed record DebtBalanceTrendPoint(
    string CurrencyCode,
    DateTime Month,
    decimal Balance,
    int AccountsWithSnapshots);

public sealed record DebtStrategyComparison(
    string CurrencyCode,
    decimal CurrentBalance,
    DateTime? SnowballDebtFreeDate,
    DateTime? AvalancheDebtFreeDate,
    decimal SnowballInterest,
    decimal AvalancheInterest,
    bool IsComplete,
    string? MissingInputs);

public sealed record DebtDashboardMilestone(
    string AccountName,
    string CurrencyCode,
    DateTime AchievedDate,
    bool IsReopened,
    DateTime? ReopenedDate);
