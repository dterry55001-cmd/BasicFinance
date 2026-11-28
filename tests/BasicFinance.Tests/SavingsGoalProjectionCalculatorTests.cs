using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class SavingsGoalProjectionCalculatorTests
{
    private readonly SavingsGoalProjectionCalculator _calculator = new();

    [Theory]
    [InlineData(SavingsScheduleFrequency.Weekly)]
    [InlineData(SavingsScheduleFrequency.Biweekly)]
    [InlineData(SavingsScheduleFrequency.Monthly)]
    [InlineData(SavingsScheduleFrequency.Quarterly)]
    [InlineData(SavingsScheduleFrequency.Yearly)]
    public void Calculate_ProjectsAllRecurringFrequencies(SavingsScheduleFrequency frequency)
    {
        var schedule = new RecurringSavingsContribution
        {
            Amount = 100m,
            Frequency = frequency,
            NextDueDate = new DateTime(2026, 12, 1),
            IsActive = true
        };

        var result = _calculator.Calculate(0m, 10000m, "USD", [schedule], null,
            new DateTime(2026, 11, 27), 12);

        Assert.True(result.Months.Sum(x => x.PlannedContributions) > 0m);
        Assert.Empty(schedule.SavingsGoal?.Contributions ?? []);
    }

    [Fact]
    public void Calculate_IncludesPlannedWithdrawalsAndOneTimeContribution()
    {
        var events = new[]
        {
            new PlannedSavingsProjectionEvent(new DateTime(2026, 12, 10), 500m, SavingsProjectionEventType.Contribution),
            new PlannedSavingsProjectionEvent(new DateTime(2027, 1, 10), 200m, SavingsProjectionEventType.Withdrawal)
        };

        var result = _calculator.Calculate(100m, 1000m, "USD", [], events,
            new DateTime(2026, 11, 27), 2);

        Assert.Equal(500m, result.Months[0].PlannedContributions);
        Assert.Equal(200m, result.Months[1].PlannedWithdrawals);
        Assert.Equal(400m, result.Months[1].ClosingBalance);
        Assert.Equal(600m, result.Months[1].RemainingAmount);
    }

    [Fact]
    public void Calculate_RespectsScheduleEndDateAndDoesNotMutateSchedule()
    {
        var schedule = new RecurringSavingsContribution
        {
            Amount = 250m,
            Frequency = SavingsScheduleFrequency.Monthly,
            NextDueDate = new DateTime(2026, 12, 1),
            EndDate = new DateTime(2027, 1, 31),
            IsActive = true
        };

        var result = _calculator.Calculate(0m, 1000m, "USD", [schedule], null,
            new DateTime(2026, 11, 27), 4);

        Assert.Equal(new[] { 250m, 250m, 0m, 0m }, result.Months.Select(x => x.PlannedContributions));
        Assert.Equal(new DateTime(2026, 12, 1), schedule.NextDueDate);
    }

    [Fact]
    public void Calculate_ReportsAlreadyFundedAndValidatesInputs()
    {
        var funded = _calculator.Calculate(1000m, 1000m, "USD", [], null,
            new DateTime(2026, 11, 27), 2);

        Assert.Equal(new DateTime(2026, 11, 27), funded.EstimatedCompletionDate);
        Assert.All(funded.Months, row => Assert.True(row.TargetReached));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.Calculate(-1m, 1000m, "USD", [], null, DateTime.Today));
        Assert.Throws<ArgumentException>(() =>
            _calculator.Calculate(0m, 1000m, "usd", [], null, DateTime.Today));
    }
}
