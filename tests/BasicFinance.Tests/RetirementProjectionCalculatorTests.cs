using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class RetirementProjectionCalculatorTests
{
    private readonly RetirementProjectionCalculator _calculator = new();

    [Fact]
    public void Calculate_ProjectsEmployeeAndCappedEmployerContributions()
    {
        var result = _calculator.Calculate(new RetirementProjectionInput(
            40, 41, 10000m, 500m, 50m, 200m, 0m, 0m, 0m, 0m));

        Assert.Equal(6000m, result.TotalEmployeeContributions);
        Assert.Equal(2400m, result.TotalEmployerContributions);
        Assert.Equal(18400m, result.ProjectedBalance);
        Assert.Equal(1, result.Years.Count);
        Assert.Equal(41, result.Years[0].Age);
    }

    [Fact]
    public void Calculate_ReportsInflationAdjustedValueAndAnnualContributionIncrease()
    {
        var result = _calculator.Calculate(new RetirementProjectionInput(
            30, 32, 0m, 100m, 0m, null, 0m, 0m, 2m, 10m));

        Assert.Equal(1200m, result.TotalEmployeeContributions);
        Assert.Equal(1320m, result.Years[1].EmployeeContributions);
        Assert.True(result.InflationAdjustedBalance < result.ProjectedBalance);
    }

    [Fact]
    public void Calculate_WithZeroContributionsAndZeroReturn_PreservesStartingBalance()
    {
        var result = _calculator.Calculate(new RetirementProjectionInput(
            55, 65, 250000m, 0m, 0m, null, 0m, 0m, 0m, 0m));

        Assert.Equal(250000m, result.ProjectedBalance);
        Assert.Equal(0m, result.TotalEmployeeContributions);
        Assert.Equal(0m, result.TotalEmployerContributions);
        Assert.Equal(0m, result.TotalInvestmentGrowth);
    }

    [Theory]
    [InlineData(17, 65)]
    [InlineData(40, 40)]
    [InlineData(40, 101)]
    public void Calculate_RejectsInvalidAges(int currentAge, int retirementAge)
    {
        var input = new RetirementProjectionInput(
            currentAge, retirementAge, 0m, 0m, 0m, null, 5m, 0m, 2m, 0m);

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(input));
    }

    [Fact]
    public void Calculate_RejectsNegativeFeesAndContributions()
    {
        var negativeContribution = new RetirementProjectionInput(
            40, 65, 0m, -1m, 0m, null, 5m, 0m, 2m, 0m);
        var negativeFee = new RetirementProjectionInput(
            40, 65, 0m, 100m, 0m, null, 5m, -1m, 2m, 0m);

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(negativeContribution));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(negativeFee));
    }
}
