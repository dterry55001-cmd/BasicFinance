using System.Text.Json;
using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;
namespace BasicFinance.Web.Services;
public sealed class CashFlowForecastSnapshotService(ApplicationDbContext database)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<CashFlowForecastSnapshot> CreateAsync(IEnumerable<RecurringCashFlow> schedules, decimal openingBalance, DateTime startDate, int months, string currencyCode = "USD", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        ValidateCurrency(currencyCode);
        if (months is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(months));
        var scheduleList = schedules.Where(x => x.IsActive && x.CurrencyCode == currencyCode).ToList();
        var rows = new CashFlowForecastService().BuildForecastForCurrency(scheduleList, openingBalance, startDate, months, currencyCode);
        var snapshot = new CashFlowForecastSnapshot
        {
            CreatedUtc = DateTime.UtcNow, ForecastStartMonth = new DateTime(startDate.Year, startDate.Month, 1),
            ForecastMonths = months, CurrencyCode = currencyCode, OpeningBalance = openingBalance, CalculationVersion = "1",
            AssumptionsJson = JsonSerializer.Serialize(new { currencyCode, openingBalance, startDate, months,
                schedules = scheduleList.Select(x => new { x.Id, x.Type, x.Amount, x.CurrencyCode, x.Frequency, x.NextDueDate, x.EndDate }) }, JsonOptions),
            ResultsJson = JsonSerializer.Serialize(rows.Select(x => new CashFlowForecastSnapshotMonth(x.Month, x.ExpectedIncome, x.ExpectedExpenses, x.NetCashFlow, x.ProjectedEndingBalance)), JsonOptions)
        };
        database.CashFlowForecastSnapshots.Add(snapshot);
        await database.SaveChangesAsync(cancellationToken);
        return snapshot;
    }
    public async Task<CashFlowForecastAccuracyResult> EvaluateAsync(int snapshotId, bool actualDataComplete = true, CancellationToken cancellationToken = default)
    {
        var snapshot = await database.CashFlowForecastSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == snapshotId, cancellationToken)
            ?? throw new KeyNotFoundException($"Forecast snapshot {snapshotId} was not found.");
        var forecast = JsonSerializer.Deserialize<List<CashFlowForecastSnapshotMonth>>(snapshot.ResultsJson, JsonOptions) ?? [];
        var results = new List<CashFlowForecastAccuracyMonth>(forecast.Count);
        foreach (var month in forecast)
        {
            var end = month.Month.AddMonths(1);
            var actuals = await database.Transactions.AsNoTracking()
                .Where(x => x.CurrencyCode == snapshot.CurrencyCode && x.TransactionDate >= month.Month && x.TransactionDate < end)
                .GroupBy(x => x.Type).Select(g => new { Type = g.Key, Amount = g.Sum(x => x.Amount) }).ToListAsync(cancellationToken);
            var income = actuals.Where(x => x.Type is TransactionType.Income or TransactionType.Deposit).Sum(x => x.Amount);
            var expenses = actuals.Where(x => x.Type is TransactionType.Expense or TransactionType.Withdrawal).Sum(x => x.Amount);
            results.Add(new CashFlowForecastAccuracyMonth(month.Month, month.ForecastIncome, income, income - month.ForecastIncome,
                month.ForecastExpenses, expenses, expenses - month.ForecastExpenses, month.ForecastNetCashFlow,
                income - expenses, income - expenses - month.ForecastNetCashFlow));
        }
        decimal? Mae(Func<CashFlowForecastAccuracyMonth, decimal> selector) =>
            actualDataComplete && results.Count > 0 ? results.Average(x => Math.Abs(selector(x))) : null;
        return new CashFlowForecastAccuracyResult(snapshot.Id, snapshot.CurrencyCode, actualDataComplete,
            Mae(x => x.IncomeVariance), Mae(x => x.ExpenseVariance), Mae(x => x.NetCashFlowVariance), results);
    }
    public Task<CashFlowForecastSnapshot?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        database.CashFlowForecastSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    private static void ValidateCurrency(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 3 || code.Any(c => !char.IsAsciiLetterUpper(c)))
            throw new ArgumentException("Currency code must be three uppercase letters.", nameof(code));
    }
}