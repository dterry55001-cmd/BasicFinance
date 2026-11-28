namespace BasicFinance.Web.Models;
public sealed class CashFlowForecastSnapshot
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ForecastStartMonth { get; set; }
    public int ForecastMonths { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal OpeningBalance { get; set; }
    public string CalculationVersion { get; set; } = "1";
    public string AssumptionsJson { get; set; } = "{}";
    public string ResultsJson { get; set; } = "[]";
}
public sealed record CashFlowForecastSnapshotMonth(DateTime Month, decimal ForecastIncome, decimal ForecastExpenses, decimal ForecastNetCashFlow, decimal ProjectedEndingBalance);
public sealed record CashFlowForecastAccuracyMonth(DateTime Month, decimal ForecastIncome, decimal ActualIncome, decimal IncomeVariance, decimal ForecastExpenses, decimal ActualExpenses, decimal ExpenseVariance, decimal ForecastNetCashFlow, decimal ActualNetCashFlow, decimal NetCashFlowVariance);
public sealed record CashFlowForecastAccuracyResult(int SnapshotId, string CurrencyCode, bool ActualDataComplete, decimal? IncomeMeanAbsoluteError, decimal? ExpenseMeanAbsoluteError, decimal? NetCashFlowMeanAbsoluteError, IReadOnlyList<CashFlowForecastAccuracyMonth> Months);
