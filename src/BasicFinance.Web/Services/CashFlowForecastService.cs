using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

public sealed class CashFlowForecastService
{
    public IReadOnlyList<CashFlowForecastMonth> BuildForecast(
        IEnumerable<RecurringCashFlow> schedules, decimal openingCashBalance, DateTime startDate, int months = 3)
    {
        if (months is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(months));
        var firstMonth = new DateTime(startDate.Year, startDate.Month, 1);
        var results = new List<CashFlowForecastMonth>();
        var balance = openingCashBalance;
        for (var offset = 0; offset < months; offset++)
        {
            var monthStart = firstMonth.AddMonths(offset);
            var monthEnd = monthStart.AddMonths(1);
            var periodStart = offset == 0 && startDate.Date > monthStart ? startDate.Date : monthStart;
            var totals = SumOccurrences(schedules, periodStart, monthEnd);
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

    private static (decimal Income, decimal Expenses) SumOccurrences(
        IEnumerable<RecurringCashFlow> schedules, DateTime periodStart, DateTime periodEndExclusive)
    {
        decimal income = 0m, expenses = 0m;
        foreach (var schedule in schedules)
        {
            if (!schedule.IsActive || schedule.CurrencyCode != "USD"
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
