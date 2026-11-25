using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class CashFlowForecastServiceTests
{
    private readonly CashFlowForecastService _service = new();

    [Fact]
    public void BuildForecast_SumsRecurringIncomeAndExpensesIntoProjectedBalance()
    {
        var schedules = new[]
        {
            new RecurringCashFlow
            {
                Type = TransactionType.Income, Amount = 3000m, CurrencyCode = "USD",
                Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 1), IsActive = true
            },
            new RecurringCashFlow
            {
                Type = TransactionType.Expense, Amount = 1200m, CurrencyCode = "USD",
                Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 5), IsActive = true
            }
        };

        var result = _service.BuildForecast(schedules, 1000m, new DateTime(2026, 12, 1), 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(3000m, result[0].ExpectedIncome);
        Assert.Equal(1200m, result[0].ExpectedExpenses);
        Assert.Equal(1800m, result[0].NetCashFlow);
        Assert.Equal(2800m, result[0].ProjectedEndingBalance);
        Assert.Equal(4600m, result[1].ProjectedEndingBalance);
    }

    [Fact]
    public void BuildForecast_ExcludesInactiveAndNonUsdSchedules()
    {
        var schedules = new[]
        {
            new RecurringCashFlow { Type = TransactionType.Income, Amount = 100m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 1), IsActive = false },
            new RecurringCashFlow { Type = TransactionType.Income, Amount = 200m, CurrencyCode = "EUR", Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 1), IsActive = true }
        };

        var result = _service.BuildForecast(schedules, 500m, new DateTime(2026, 12, 1), 1);

        Assert.Equal(0m, result[0].ExpectedIncome);
        Assert.Equal(500m, result[0].ProjectedEndingBalance);
    }

    [Fact]
    public void BuildForecast_StopsAtScheduleEndDate()
    {
        var schedules = new[]
        {
            new RecurringCashFlow
            {
                Type = TransactionType.Expense, Amount = 100m, CurrencyCode = "USD",
                Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 1),
                EndDate = new DateTime(2026, 12, 31), IsActive = true
            }
        };

        var result = _service.BuildForecast(schedules, 500m, new DateTime(2026, 12, 1), 2);

        Assert.Equal(100m, result[0].ExpectedExpenses);
        Assert.Equal(0m, result[1].ExpectedExpenses);
    }

    [Fact]
    public void BuildForecast_RejectsInvalidForecastHorizon()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.BuildForecast([], 0m, DateTime.Today, 0));
    }
}
