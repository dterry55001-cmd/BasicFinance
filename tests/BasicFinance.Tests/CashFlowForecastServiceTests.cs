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
    [Fact]
    public void BuildDailyForecast_PlacesOccurrencesInTheirExactDayAndCarriesBalanceForward()
    {
        var schedules = new[]
        {
            new RecurringCashFlow { Type = TransactionType.Income, Amount = 500m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Weekly, NextDueDate = new DateTime(2026, 12, 2), IsActive = true },
            new RecurringCashFlow { Type = TransactionType.Expense, Amount = 75m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 3), IsActive = true }
        };
        var result = _service.BuildDailyForecast(schedules, 100m, new DateTime(2026, 12, 1), 4);
        Assert.Equal(4, result.Count);
        Assert.Equal(new DateTime(2026, 12, 1), result[0].StartDate);
        Assert.Equal(0m, result[0].ExpectedIncome);
        Assert.Equal(500m, result[1].ExpectedIncome);
        Assert.Equal(75m, result[2].ExpectedExpenses);
        Assert.Equal(525m, result[3].ProjectedEndingBalance);
    }

    [Fact]
    public void BuildWeeklyForecast_IncludesOccurrencesOnFirstAndLastDaysOfWeek()
    {
        var schedules = new[]
        {
            new RecurringCashFlow { Type = TransactionType.Income, Amount = 100m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Weekly, NextDueDate = new DateTime(2026, 12, 1), IsActive = true },
            new RecurringCashFlow { Type = TransactionType.Expense, Amount = 25m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Weekly, NextDueDate = new DateTime(2026, 12, 7), IsActive = true }
        };
        var result = _service.BuildWeeklyForecast(schedules, 50m, new DateTime(2026, 12, 1), 2);
        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2026, 12, 1), result[0].StartDate);
        Assert.Equal(new DateTime(2026, 12, 7), result[0].EndDate);
        Assert.Equal(100m, result[0].ExpectedIncome);
        Assert.Equal(25m, result[0].ExpectedExpenses);
        Assert.Equal(125m, result[0].ProjectedEndingBalance);
        Assert.Equal(100m, result[1].ExpectedIncome);
    }

    [Fact]
    public void BuildDailyForecast_RespectsInclusiveScheduleEndDate()
    {
        var schedule = new RecurringCashFlow
        {
            Type = TransactionType.Expense, Amount = 10m, CurrencyCode = "USD",
            Frequency = CashFlowFrequency.Weekly, NextDueDate = new DateTime(2026, 12, 1),
            EndDate = new DateTime(2026, 12, 8), IsActive = true
        };
        var result = _service.BuildDailyForecast([schedule], 100m, new DateTime(2026, 12, 1), 15);
        Assert.Equal(10m, result[0].ExpectedExpenses);
        Assert.Equal(10m, result[7].ExpectedExpenses);
        Assert.All(result.Skip(8), item => Assert.Equal(0m, item.ExpectedExpenses));
    }

    [Fact]
    public void BuildDailyAndWeeklyForecast_RejectInvalidHorizons()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.BuildDailyForecast([], 0m, DateTime.Today, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.BuildDailyForecast([], 0m, DateTime.Today, 91));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.BuildWeeklyForecast([], 0m, DateTime.Today, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.BuildWeeklyForecast([], 0m, DateTime.Today, 53));
    }

    [Theory]
    [InlineData(CashFlowFrequency.Weekly, 5)]
    [InlineData(CashFlowFrequency.Biweekly, 3)]
    [InlineData(CashFlowFrequency.Monthly, 1)]
    [InlineData(CashFlowFrequency.Quarterly, 1)]
    [InlineData(CashFlowFrequency.Yearly, 1)]
    public void BuildForecast_SupportsEveryRecurringFrequency(CashFlowFrequency frequency, int expectedOccurrences)
    {
        var schedule = new RecurringCashFlow
        {
            Type = TransactionType.Income, Amount = 10m, CurrencyCode = "USD",
            Frequency = frequency, NextDueDate = new DateTime(2026, 12, 1), IsActive = true
        };

        var result = _service.BuildForecast([schedule], 0m, new DateTime(2026, 12, 1), 1);

        Assert.Equal(expectedOccurrences * 10m, result[0].ExpectedIncome);
    }

    [Fact]
    public void BuildActualVsForecast_ComparesScheduledAndPostedCashFlows()
    {
        var schedules = new[]
        {
            new RecurringCashFlow { Type = TransactionType.Income, Amount = 1000m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 1), IsActive = true },
            new RecurringCashFlow { Type = TransactionType.Expense, Amount = 400m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 5), IsActive = true }
        };
        var transactions = new[]
        {
            new FinancialTransaction { Type = TransactionType.Income, Amount = 900m, CurrencyCode = "USD", TransactionDate = new DateTime(2026, 12, 2) },
            new FinancialTransaction { Type = TransactionType.Expense, Amount = 450m, CurrencyCode = "USD", TransactionDate = new DateTime(2026, 12, 6) },
            new FinancialTransaction { Type = TransactionType.Transfer, Amount = 300m, CurrencyCode = "USD", TransactionDate = new DateTime(2026, 12, 7) },
            new FinancialTransaction { Type = TransactionType.Income, Amount = 500m, CurrencyCode = "EUR", TransactionDate = new DateTime(2026, 12, 8) }
        };

        var result = _service.BuildActualVsForecast(schedules, transactions, new DateTime(2026, 12, 1), 1);

        Assert.Single(result);
        Assert.Equal(1000m, result[0].ForecastIncome);
        Assert.Equal(900m, result[0].ActualIncome);
        Assert.Equal(-100m, result[0].IncomeVariance);
        Assert.Equal(400m, result[0].ForecastExpenses);
        Assert.Equal(450m, result[0].ActualExpenses);
        Assert.Equal(50m, result[0].ExpenseVariance);
    }

    [Fact]
    public void BuildForecast_ExcludesOccurrencesBeforeStartDateInFirstMonth()
    {
        var schedules = new[]
        {
            new RecurringCashFlow { Type = TransactionType.Expense, Amount = 100m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 1), IsActive = true },
            new RecurringCashFlow { Type = TransactionType.Income, Amount = 500m, CurrencyCode = "USD", Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 10), IsActive = true }
        };

        var result = _service.BuildForecast(schedules, 100m, new DateTime(2026, 12, 5), 1);

        Assert.Equal(500m, result[0].ExpectedIncome);
        Assert.Equal(0m, result[0].ExpectedExpenses);
        Assert.Equal(600m, result[0].ProjectedEndingBalance);
    }

    [Fact]
    public void BuildActualVsForecast_SeparatesTransactionsByCalendarMonthAndExcludesTransfers()
    {
        var schedules = new[]
        {
            new RecurringCashFlow
            {
                Type = TransactionType.Income, Amount = 100m, CurrencyCode = "USD",
                Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 1), IsActive = true
            },
            new RecurringCashFlow
            {
                Type = TransactionType.Expense, Amount = 40m, CurrencyCode = "USD",
                Frequency = CashFlowFrequency.Monthly, NextDueDate = new DateTime(2026, 12, 5), IsActive = true
            }
        };
        var transactions = new[]
        {
            new FinancialTransaction { Type = TransactionType.Income, Amount = 80m, CurrencyCode = "USD", TransactionDate = new DateTime(2026, 12, 31) },
            new FinancialTransaction { Type = TransactionType.Expense, Amount = 50m, CurrencyCode = "USD", TransactionDate = new DateTime(2027, 1, 1) },
            new FinancialTransaction { Type = TransactionType.Transfer, Amount = 500m, CurrencyCode = "USD", TransactionDate = new DateTime(2026, 12, 15) }
        };

        var result = _service.BuildActualVsForecast(schedules, transactions, new DateTime(2026, 12, 20), 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(80m, result[0].ActualIncome);
        Assert.Equal(0m, result[0].ActualExpenses);
        Assert.Equal(0m, result[1].ActualIncome);
        Assert.Equal(50m, result[1].ActualExpenses);
    }

    [Fact]
    public void BuildForecast_ExcludesScheduleWhenFirstDueDateIsAfterItsEndDate()
    {
        var schedule = new RecurringCashFlow
        {
            Type = TransactionType.Expense, Amount = 125m, CurrencyCode = "USD",
            Frequency = CashFlowFrequency.Weekly, NextDueDate = new DateTime(2026, 12, 8),
            EndDate = new DateTime(2026, 12, 7), IsActive = true
        };

        var result = _service.BuildForecast([schedule], 500m, new DateTime(2026, 12, 1), 1);

        Assert.Equal(0m, result[0].ExpectedExpenses);
        Assert.Equal(500m, result[0].ProjectedEndingBalance);
    }

    [Fact]
    public void BuildForecast_SkipsScheduleThatExpiredYearsBeforeForecastWindow()
    {
        var schedule = new RecurringCashFlow
        {
            Type = TransactionType.Expense,
            Amount = 125m,
            CurrencyCode = "USD",
            Frequency = CashFlowFrequency.Monthly,
            NextDueDate = new DateTime(2010, 1, 1),
            EndDate = new DateTime(2010, 12, 31),
            IsActive = true
        };

        var result = _service.BuildForecast([schedule], 500m, new DateTime(2026, 12, 1), 1);

        Assert.Equal(0m, result[0].ExpectedExpenses);
        Assert.Equal(500m, result[0].ProjectedEndingBalance);
    }

    [Fact]
    public void BuildForecast_SkipsExpiredScheduleBeforeValidatingItsFrequency()
    {
        var schedule = new RecurringCashFlow
        {
            Type = TransactionType.Expense,
            Amount = 125m,
            CurrencyCode = "USD",
            Frequency = (CashFlowFrequency)999,
            NextDueDate = new DateTime(2010, 1, 1),
            EndDate = new DateTime(2010, 12, 31),
            IsActive = true
        };

        var result = _service.BuildForecast([schedule], 500m, new DateTime(2026, 12, 1), 1);

        Assert.Equal(0m, result[0].ExpectedExpenses);
    }

    [Fact]
    public void BuildForecast_RejectsUnknownRecurringFrequencyWhenOccurrenceIsDue()
    {
        var schedule = new RecurringCashFlow
        {
            Type = TransactionType.Income, Amount = 100m, CurrencyCode = "USD",
            Frequency = (CashFlowFrequency)999, NextDueDate = new DateTime(2026, 12, 1), IsActive = true
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.BuildForecast([schedule], 0m, new DateTime(2026, 12, 1), 1));
    }


    [Fact]
    public void BuildVariableForecast_UsesCompletedMonthsAndExcludesTransfersAndOtherCurrencies()
    {
        var transactions = new List<FinancialTransaction>();
        for (var month = 1; month <= 6; month++)
        {
            transactions.Add(new FinancialTransaction
            {
                Type = TransactionType.Income, Amount = month * 100m, CurrencyCode = "USD",
                TransactionDate = new DateTime(2026, month, 10)
            });
            transactions.Add(new FinancialTransaction
            {
                Type = TransactionType.Expense, Amount = 50m, CurrencyCode = "USD",
                TransactionDate = new DateTime(2026, month, 15)
            });
        }
        transactions.Add(new FinancialTransaction
        {
            Type = TransactionType.Transfer, Amount = 9000m, CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 6, 20)
        });
        transactions.Add(new FinancialTransaction
        {
            Type = TransactionType.Income, Amount = 8000m, CurrencyCode = "EUR",
            TransactionDate = new DateTime(2026, 6, 20)
        });
        transactions.Add(new FinancialTransaction
        {
            Type = TransactionType.Income, Amount = 50000m, CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 7, 1)
        });

        var result = _service.BuildVariableForecast(
            transactions, new DateTime(2026, 7, 1), 3, "USD", 6);

        Assert.Equal(ForecastConfidence.Medium, result.Confidence);
        Assert.Equal(6, result.HistoricalMonths);
        Assert.Equal(350m, result.AverageMonthlyIncome);
        Assert.Equal(50m, result.AverageMonthlyExpenses);
        Assert.Equal(3, result.Months.Count);
        Assert.Equal(100m, result.Months[0].HistoricalLowIncome);
        Assert.Equal(600m, result.Months[0].HistoricalHighIncome);
    }

    [Fact]
    public void BuildVariableForecast_ReportsLowConfidenceForShortHistoryAndSupportsOtherCurrencies()
    {
        var transactions = new[]
        {
            new FinancialTransaction
            {
                Type = TransactionType.Income, Amount = 250m, CurrencyCode = "EUR",
                TransactionDate = new DateTime(2026, 10, 10)
            }
        };

        var result = _service.BuildVariableForecast(
            transactions, new DateTime(2026, 11, 1), 2, "EUR", 3);

        Assert.Equal(ForecastConfidence.Low, result.Confidence);
        Assert.Equal(3, result.HistoricalMonths);
        Assert.Equal(250m / 3m, result.AverageMonthlyIncome);
        Assert.Equal(0m, result.AverageMonthlyExpenses);
    }

    [Fact]
    public void BuildVariableForecast_RejectsInvalidCurrencyAndHorizons()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.BuildVariableForecast([], DateTime.Today, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.BuildVariableForecast([], DateTime.Today, 1, "USD", 0));
        Assert.Throws<ArgumentException>(() =>
            _service.BuildVariableForecast([], DateTime.Today, 1, "usd", 6));
    }


}
