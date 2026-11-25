using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class BudgetRolloverCalculatorTests
{
    [Fact]
    public void CalculateNextMonth_CarriesUnusedBudgetForward()
    {
        var result = BudgetRolloverCalculator.CalculateNextMonth(
            plannedAmount: 500m, currentRollover: 25m, actualSpending: 400m, rolloverEnabled: true);

        Assert.Equal(125m, result);
    }

    [Fact]
    public void CalculateNextMonth_CarriesOverspendingAsNegativeRollover()
    {
        var result = BudgetRolloverCalculator.CalculateNextMonth(
            plannedAmount: 500m, currentRollover: 0m, actualSpending: 650m, rolloverEnabled: true);

        Assert.Equal(-150m, result);
    }

    [Fact]
    public void CalculateNextMonth_ReturnsZeroWhenRolloverIsDisabled()
    {
        var result = BudgetRolloverCalculator.CalculateNextMonth(
            plannedAmount: 500m, currentRollover: 75m, actualSpending: 400m, rolloverEnabled: false);

        Assert.Equal(0m, result);
    }

    [Theory]
    [InlineData(-1, 0, 0, true)]
    [InlineData(100, 0, -1, true)]
    public void CalculateNextMonth_RejectsNegativeBudgetOrSpending(
        decimal planned, decimal rollover, decimal actual, bool enabled)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BudgetRolloverCalculator.CalculateNextMonth(planned, rollover, actual, enabled));
    }
}
