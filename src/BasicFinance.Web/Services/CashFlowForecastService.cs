using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

public sealed class CashFlowForecastService
{
    public IReadOnlyList<CashFlowForecastMonth> BuildForecast(
        IEnumerable<RecurringCashFlow> schedules, decimal openingCashBalance, DateTime startDate, int months = 3)
        => BuildForecastForCurrency(schedules, openingCashBalance, startDate, months, "USD");

    public IReadOnlyList<CashFlowForecastMonth> BuildForecastForCurrency(
        IEnumerable<RecurringCashFlow> schedules, decimal openingCashBalance, DateTime startDate,
        int months, string currencyCode)
    {
        if (months is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(months));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3 ||
            currencyCode.Any(c => !char.IsAsciiLetterUpper(c)))
            throw new ArgumentException("Currency code must be three uppercase letters.", nameof(currencyCode));
        var firstMonth = new DateTime(startDate.Year, startDate.Month, 1);
        var results = new List<CashFlowForecastMonth>();
        var balance = openingCashBalance;
        for (var offset = 0; offset < months; offset++)
        {
            var monthStart = firstMonth.AddMonths(offset);
            var monthEnd = monthStart.AddMonths(1);
            var periodStart = offset == 0 && startDate.Date > monthStart ? startDate.Date : monthStart;
            var totals = SumOccurrences(schedules, periodStart, monthEnd, currencyCode);
            var net = totals.Income - totals.Expenses;
            balance += net;
            results.Add(new CashFlowForecastMonth(monthStart, totals.Income, totals.Expenses, net, balance));
        }
        return results;
    }

    public IReadOnlyList<CashFlowForecastPeriod> BuildDailyForecast(
        IEnumerable<RecurringCashFlow> schedules, decimal openingCashBalance, DateTime startDate, int days = 30)
    {
        if (days is < 1 or > 90) throw new ArgumentOutOfRangeException(nameof(days));
        return BuildPeriods(schedules, openingCashBalance, startDate.Date, days, 1);
    }

    public IReadOnlyList<CashFlowForecastPeriod> BuildWeeklyForecast(
        IEnumerable<RecurringCashFlow> schedules, decimal openingCashBalance, DateTime startDate, int weeks = 12)
    {
        if (weeks is < 1 or > 52) throw new ArgumentOutOfRangeException(nameof(weeks));
        return BuildPeriods(schedules, openingCashBalance, startDate.Date, weeks, 7);
    }

    public IReadOnlyList<CashFlowActualVsForecastMonth> BuildActualVsForecast(
        IEnumerable<RecurringCashFlow> schedules,
        IEnumerable<FinancialTransaction> transactions,
        DateTime startDate,
        int months = 6)
    {
        if (months is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(months));
        var firstMonth = new DateTime(startDate.Year, startDate.Month, 1);
        var results = new List<CashFlowActualVsForecastMonth>(months);
        for (var offset = 0; offset < months; offset++)
        {
            var monthStart = firstMonth.AddMonths(offset);
            var monthEnd = monthStart.AddMonths(1);
            var expected = SumOccurrences(schedules, monthStart, monthEnd);
            var actualIncome = transactions
                .Where(x => x.CurrencyCode == "USD" && x.TransactionDate >= monthStart && x.TransactionDate < monthEnd
                    && x.Type is TransactionType.Income or TransactionType.Deposit)
                .Sum(x => x.Amount);
            var actualExpenses = transactions
                .Where(x => x.CurrencyCode == "USD" && x.TransactionDate >= monthStart && x.TransactionDate < monthEnd
                    && x.Type is TransactionType.Expense or TransactionType.Withdrawal)
                .Sum(x => x.Amount);
            results.Add(new CashFlowActualVsForecastMonth(
                monthStart, expected.Income, actualIncome, actualIncome - expected.Income,
                expected.Expenses, actualExpenses, actualExpenses - expected.Expenses));
        }
        return results;
    }

    private static IReadOnlyList<CashFlowForecastPeriod> BuildPeriods(
        IEnumerable<RecurringCashFlow> schedules, decimal openingCashBalance, DateTime startDate, int count, int daysPerPeriod)
    {
        var periods = new List<CashFlowForecastPeriod>(count);
        var balance = openingCashBalance;
        for (var index = 0; index < count; index++)
        {
            var periodStart = startDate.AddDays((long)index * daysPerPeriod);
            var periodEnd = periodStart.AddDays(daysPerPeriod);
            var totals = SumOccurrences(schedules, periodStart, periodEnd);
            var net = totals.Income - totals.Expenses;
            balance += net;
            periods.Add(new CashFlowForecastPeriod(periodStart, periodEnd.AddDays(-1), totals.Income, totals.Expenses, net, balance));
        }
        return periods;
    }


    /// <summary>
    /// Estimates variable income and expenses from completed historical calendar months.
    /// Transfers are excluded. Estimates remain separate from recurring schedules and the ledger.
    /// </summary>
    public VariableCashFlowForecast BuildVariableForecast(
        IEnumerable<FinancialTransaction> transactions,
        DateTime forecastStartDate,
        int forecastMonths = 6,
        string currencyCode = "USD",
        int historyMonths = 6)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        if (forecastMonths is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(forecastMonths));
        if (historyMonths is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(historyMonths));
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3 ||
            currencyCode.Any(c => !char.IsAsciiLetterUpper(c)))
            throw new ArgumentException("Currency code must be three uppercase letters.", nameof(currencyCode));

        var firstForecastMonth = new DateTime(forecastStartDate.Year, forecastStartDate.Month, 1);
        var historyStart = firstForecastMonth.AddMonths(-historyMonths);
        var incomeByMonth = Enumerable.Range(0, historyMonths).ToDictionary(
            offset => historyStart.AddMonths(offset), _ => 0m);
        var expensesByMonth = Enumerable.Range(0, historyMonths).ToDictionary(
            offset => historyStart.AddMonths(offset), _ => 0m);

        foreach (var transaction in transactions)
        {
            if (transaction.CurrencyCode != currencyCode ||
                transaction.TransactionDate < historyStart ||
                transaction.TransactionDate >= firstForecastMonth)
                continue;

            var month = new DateTime(transaction.TransactionDate.Year, transaction.TransactionDate.Month, 1);
            if (transaction.Type is TransactionType.Income or TransactionType.Deposit)
                incomeByMonth[month] += transaction.Amount;
            else if (transaction.Type is TransactionType.Expense or TransactionType.Withdrawal)
                expensesByMonth[month] += transaction.Amount;
        }

        var averageIncome = incomeByMonth.Values.Average();
        var averageExpenses = expensesByMonth.Values.Average();
        var confidence = historyMonths >= 12 ? ForecastConfidence.High
            : historyMonths >= 6 ? ForecastConfidence.Medium
            : ForecastConfidence.Low;
        var months = Enumerable.Range(0, forecastMonths).Select(offset =>
            new VariableCashFlowForecastMonth(
                firstForecastMonth.AddMonths(offset),
                decimal.Round(averageIncome, 2, MidpointRounding.AwayFromZero),
                decimal.Round(averageExpenses, 2, MidpointRounding.AwayFromZero),
                incomeByMonth.Values.Min(),
                incomeByMonth.Values.Max(),
                expensesByMonth.Values.Min(),
                expensesByMonth.Values.Max())).ToArray();

        return new VariableCashFlowForecast(
            currencyCode, historyStart, firstForecastMonth.AddDays(-1),
            historyMonths, confidence, averageIncome, averageExpenses, months);
    }

    private static (decimal Income, decimal Expenses) SumOccurrences(
        IEnumerable<RecurringCashFlow> schedules, DateTime periodStart, DateTime periodEndExclusive, string currencyCode = "USD")
    {
        decimal income = 0m, expenses = 0m;
        foreach (var schedule in schedules)
        {
            if (!schedule.IsActive || schedule.CurrencyCode != currencyCode
                || schedule.Type is not (TransactionType.Income or TransactionType.Expense))
                continue;

            var due = schedule.NextDueDate.Date;
            var endDate = schedule.EndDate?.Date;

            // An expired schedule cannot contribute to this period. Skip it before
            // advancing old dates, avoiding potentially thousands of useless iterations.
            if (endDate.HasValue && (endDate.Value < periodStart.Date || due > endDate.Value))
                continue;

            while (due < periodStart && due < periodEndExclusive
                && (!endDate.HasValue || due <= endDate.Value))
            {
                due = NextDate(due, schedule.Frequency);
            }

            while (due >= periodStart && due < periodEndExclusive
                && (!endDate.HasValue || due <= endDate.Value))
            {
                if (schedule.Type == TransactionType.Income) income += schedule.Amount;
                else expenses += schedule.Amount;
                due = NextDate(due, schedule.Frequency);
            }
        }
        return (income, expenses);
    }

    private static DateTime NextDate(DateTime date, CashFlowFrequency frequency) => frequency switch
    {
        CashFlowFrequency.Weekly => date.AddDays(7),
        CashFlowFrequency.Biweekly => date.AddDays(14),
        CashFlowFrequency.Monthly => date.AddMonths(1),
        CashFlowFrequency.Quarterly => date.AddMonths(3),
        CashFlowFrequency.Yearly => date.AddYears(1),
        _ => throw new ArgumentOutOfRangeException(nameof(frequency))
    };
}

public sealed record CashFlowForecastMonth(DateTime Month, decimal ExpectedIncome, decimal ExpectedExpenses, decimal NetCashFlow, decimal ProjectedEndingBalance);
public sealed record CashFlowForecastPeriod(DateTime StartDate, DateTime EndDate, decimal ExpectedIncome, decimal ExpectedExpenses, decimal NetCashFlow, decimal ProjectedEndingBalance);

public sealed record CashFlowActualVsForecastMonth(DateTime Month, decimal ForecastIncome, decimal ActualIncome, decimal IncomeVariance, decimal ForecastExpenses, decimal ActualExpenses, decimal ExpenseVariance);

public enum ForecastConfidence { Low, Medium, High }

public sealed record VariableCashFlowForecastMonth(
    DateTime Month,
    decimal ExpectedVariableIncome,
    decimal ExpectedVariableExpenses,
    decimal HistoricalLowIncome,
    decimal HistoricalHighIncome,
    decimal HistoricalLowExpenses,
    decimal HistoricalHighExpenses);

public sealed record VariableCashFlowForecast(
    string CurrencyCode,
    DateTime HistoryStart,
    DateTime HistoryEnd,
    int HistoricalMonths,
    ForecastConfidence Confidence,
    decimal AverageMonthlyIncome,
    decimal AverageMonthlyExpenses,
    IReadOnlyList<VariableCashFlowForecastMonth> Months);
