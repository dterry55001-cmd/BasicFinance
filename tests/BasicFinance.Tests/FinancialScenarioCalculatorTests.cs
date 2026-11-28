using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class FinancialScenarioCalculatorTests
{
    private readonly FinancialScenarioCalculator _calculator = new();

    [Fact]
    public void Compare_ChangesCashFlowDebtAndSavingsWithoutMutatingInput()
    {
        var input = new FinancialScenarioInput(
            10000m, 12m, 300m, 2000m, 200m, 5000m, 3500m, 12,
            new FinancialScenarioAssumptions());
        var scenario = new FinancialScenarioAssumptions(
            MonthlyIncomeChange: 500m,
            MonthlyExpenseChange: -200m,
            AnnualDebtRateChange: -2m,
            MonthlyDebtPaymentChange: 100m,
            MonthlySavingsContributionChange: 100m);

        var result = _calculator.Compare(input, scenario, scenarioName: "Raise and expense reduction");

        Assert.Equal(12, result.Baseline.Months.Count);
        Assert.True(result.Scenario.EndingDebtBalance < result.Baseline.EndingDebtBalance);
        Assert.True(result.Scenario.EndingSavingsBalance > result.Baseline.EndingSavingsBalance);
        Assert.True(result.Scenario.CumulativeNetCashFlow > result.Baseline.CumulativeNetCashFlow);
        Assert.Equal(10000m, input.StartingDebtBalance);
        Assert.Equal(2000m, input.StartingSavingsBalance);
    }

    [Fact]
    public void Compare_AppliesOneTimeEventsOnlyInSelectedMonth()
    {
        var input = new FinancialScenarioInput(
            0m, 0m, 0m, 0m, 0m, 1000m, 500m, 3,
            new FinancialScenarioAssumptions());
        var events = new[]
        {
            new FinancialScenarioEvent(2, IncomeChange: 2000m, ExpenseChange: 100m,
                SavingsContributionChange: 300m)
        };

        var result = _calculator.Compare(input, new FinancialScenarioAssumptions(), events);

        Assert.Equal(1000m, result.Scenario.Months[0].Income);
        Assert.Equal(3000m, result.Scenario.Months[1].Income);
        Assert.Equal(300m, result.Scenario.Months[1].SavingsContribution);
        Assert.Equal(1000m, result.Scenario.Months[2].Income);
    }

    [Fact]
    public void Compare_ReportsDebtFreeMonthAndNeverCreatesNegativeBalances()
    {
        var input = new FinancialScenarioInput(
            100m, 0m, 150m, 0m, 0m, 1000m, 0m, 2,
            new FinancialScenarioAssumptions());

        var result = _calculator.Compare(input, new FinancialScenarioAssumptions());

        Assert.Equal(1, result.Baseline.DebtFreeMonth);
        Assert.Equal(0m, result.Baseline.EndingDebtBalance);
        Assert.All(result.Baseline.Months, row =>
        {
            Assert.True(row.ClosingDebtBalance >= 0m);
            Assert.True(row.ClosingSavingsBalance >= 0m);
        });
    }

    [Fact]
    public void Compare_RejectsInvalidInputsAndEventsOutsideHorizon()
    {
        var invalid = new FinancialScenarioInput(
            0m, 101m, 100m, 0m, 0m, 1000m, 500m, 3,
            new FinancialScenarioAssumptions());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.Compare(invalid, new FinancialScenarioAssumptions()));

        var valid = new FinancialScenarioInput(
            0m, 0m, 100m, 0m, 0m, 1000m, 500m, 3,
            new FinancialScenarioAssumptions());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.Compare(valid, new FinancialScenarioAssumptions(),
                [new FinancialScenarioEvent(4)]));
    }
}
