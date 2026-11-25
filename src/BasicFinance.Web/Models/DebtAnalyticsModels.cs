namespace BasicFinance.Web.Models;

public sealed class DebtAnalyticsViewModel
{
    public List<DebtAnalyticsCurrencySummary> CurrencySummaries { get; set; } = [];
    public List<DebtMonthlyAnalyticsRow> MonthlyBreakdown { get; set; } = [];
    public List<CreditUtilizationTrendRow> UtilizationTrends { get; set; } = [];
    public List<DebtAnnualReportRow> AnnualReports { get; set; } = [];
    public int SelectedYear { get; set; }
    public int AvailableYearsStart { get; set; }
    public int AvailableYearsEnd { get; set; }
}

public sealed record DebtAnalyticsCurrencySummary(
    string CurrencyCode,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal FeesPaid,
    decimal TotalPaid,
    decimal InterestToPrincipalPercent,
    int PaymentCount);

public sealed record DebtMonthlyAnalyticsRow(
    string CurrencyCode,
    DateTime Month,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal FeesPaid,
    decimal TotalPaid,
    int PaymentCount);

public sealed record CreditUtilizationTrendRow(
    string CurrencyCode,
    DateTime Month,
    decimal ReportedBalance,
    decimal ReportedLimit,
    decimal UtilizationPercent,
    int AccountsReported);

public sealed record DebtAnnualReportRow(
    string CurrencyCode,
    int Year,
    decimal PrincipalPaid,
    decimal InterestPaid,
    decimal FeesPaid,
    decimal TotalPaid,
    int PaymentCount);
