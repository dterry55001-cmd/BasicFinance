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
            decimal income = 0m, expenses = 0m;
            foreach (var schedule in schedules.Where(x => x.IsActive && x.CurrencyCode == "USD"))
            {
                var due = schedule.NextDueDate.Date;
                while (due < monthStart && due <= monthEnd)
                    due = NextDate(due, schedule.Frequency);
                while (due >= monthStart && due < monthEnd && (!schedule.EndDate.HasValue || due <= schedule.EndDate.Value.Date))
                {
                    if (schedule.Type == TransactionType.Income) income += schedule.Amount;
                    else if (schedule.Type == TransactionType.Expense) expenses += schedule.Amount;
                    due = NextDate(due, schedule.Frequency);
                }
            }
            var net = income - expenses;
            balance += net;
            results.Add(new CashFlowForecastMonth(monthStart, income, expenses, net, balance));
        }
        return results;
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
