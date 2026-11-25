using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class DebtManagementServiceTests
{
    private readonly DebtManagementService _service = new();

    [Fact]
    public void Amortization_ZeroInterest_DistributesPrincipalAcrossTerm()
    {
        var result = _service.CalculateAmortization(new LoanAmortizationInput
        {
            Principal = 1200m,
            AnnualRatePercent = 0m,
            TermMonths = 12,
            FirstPaymentDate = new DateTime(2026, 1, 15)
        });

        Assert.Equal(100m, result.RegularPayment);
        Assert.Equal(0m, result.TotalInterest);
        Assert.Equal(1200m, result.TotalPaid);
        Assert.Equal(12, result.Payments.Count);
        Assert.Equal(0m, result.Payments[^1].RemainingBalance);
    }

    [Fact]
    public void Amortization_PositiveRate_CalculatesMonthlyInterestAndPaysOff()
    {
        var result = _service.CalculateAmortization(new LoanAmortizationInput
        {
            Principal = 1000m,
            AnnualRatePercent = 12m,
            TermMonths = 12,
            FirstPaymentDate = new DateTime(2026, 1, 1)
        });

        Assert.Equal(10m, result.Payments[0].Interest);
        Assert.True(result.TotalInterest > 0m);
        Assert.Equal(0m, result.Payments[^1].RemainingBalance);
        Assert.Equal(12, result.Payments.Count);
    }

    [Fact]
    public void PayoffPlanner_ReturnsSnowballAndAvalancheScenarios()
    {
        var debts = new[]
        {
            new DebtPayoffDebtInput { AccountId = 1, Name = "Small balance", Balance = 500m, AnnualRatePercent = 8m, MinimumPayment = 25m },
            new DebtPayoffDebtInput { AccountId = 2, Name = "High APR", Balance = 3000m, AnnualRatePercent = 24m, MinimumPayment = 75m }
        };

        var snowball = _service.CalculatePayoff(debts, 100m, avalanche: false, startDate: new DateTime(2026, 1, 1));
        var avalanche = _service.CalculatePayoff(debts, 100m, avalanche: true, startDate: new DateTime(2026, 1, 1));

        Assert.Equal("Snowball", snowball.Strategy);
        Assert.Equal("Avalanche", avalanche.Strategy);
        Assert.True(snowball.ReachedDebtFree);
        Assert.True(avalanche.ReachedDebtFree);
        Assert.Equal(2, snowball.Debts.Count);
        Assert.True(avalanche.TotalInterest <= snowball.TotalInterest);
    }


    [Fact]
    public void PayoffPlanner_CanUseEnteredMinimumPaymentsWithoutFallbackFloor()
    {
        var debts = new[]
        {
            new DebtPayoffDebtInput { AccountId = 1, Name = "Card", Balance = 1200m, AnnualRatePercent = 0m, MinimumPayment = 1m }
        };

        var exactMinimum = _service.CalculatePayoff(debts, 0m, avalanche: true,
            startDate: new DateTime(2026, 1, 1), applyMinimumPaymentFloor: false);
        var floored = _service.CalculatePayoff(debts, 0m, avalanche: true,
            startDate: new DateTime(2026, 1, 1), applyMinimumPaymentFloor: true);

        Assert.True(exactMinimum.ReachedDebtFree);
        Assert.True(floored.ReachedDebtFree);
        Assert.True(exactMinimum.MonthsToPayoff > floored.MonthsToPayoff);
    }

    [Fact]
    public void AccountPayoff_ProducesDecliningBalanceScheduleAndFinalZero()
    {
        var result = _service.CalculateAccountPayoff(1200m, 12m, 100m, 25m,
            new DateTime(2026, 1, 1));

        Assert.Equal(1200m, result.StartingBalance);
        Assert.Equal(125m, result.MonthlyMinimum + result.ExtraMonthlyPayment);
        Assert.True(result.TotalInterest > 0m);
        Assert.Equal(0m, result.Payments[^1].RemainingBalance);
        Assert.Equal(result.Payments[^1].PaymentDate, result.PayoffDate);
        Assert.All(result.Payments, row => Assert.True(row.Principal > 0m));
    }

    [Fact]
    public void AccountPayoff_RejectsPaymentThatDoesNotCoverInterest()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.CalculateAccountPayoff(10000m, 24m, 10m, 0m));
    }

    [Fact]
    public void Amortization_RejectsInvalidTerm()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.CalculateAmortization(new LoanAmortizationInput
        {
            Principal = 1000m,
            AnnualRatePercent = 5m,
            TermMonths = 0
        }));
    }
    [Fact]
    public void EarlyPayoffScenarios_CompareLumpMonthlyAndCombinedPayments()
    {
        var scenarios = _service.CalculateEarlyPayoffScenarios(5000m, 18m, 150m,
            500m, 2, 50m, new DateTime(2026, 1, 1));

        Assert.Equal(4, scenarios.Count);
        Assert.Equal("Current payment", scenarios[0].Strategy);
        Assert.True(scenarios[1].InterestSavings >= 0m);
        Assert.True(scenarios[2].InterestSavings >= 0m);
        Assert.True(scenarios[3].InterestSavings >= scenarios[1].InterestSavings);
        Assert.True(scenarios[3].MonthsToPayoff <= scenarios[0].MonthsToPayoff);
        Assert.Equal(0m, scenarios[3].Payments[^1].RemainingBalance);
    }

    [Fact]
    public void EarlyPayoffScenarios_RejectPaymentBelowMonthlyInterest()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.CalculateEarlyPayoffScenarios(10000m, 24m, 10m, 0m, 1, 0m));
    }

    [Fact]
    public void DebtToIncomeRecord_CalculatesHousingAndTotalRatiosSeparately()
    {
        var record = new DebtToIncomeRecord
        {
            GrossMonthlyIncome = 6000m,
            HousingMonthlyPayment = 1800m,
            OtherMonthlyDebtPayment = 600m
        };

        Assert.Equal(30m, record.HousingRatioPercent);
        Assert.Equal(40m, record.TotalDebtRatioPercent);
        Assert.Equal(2400m, record.TotalMonthlyDebtObligations);
    }

    [Fact]
    public void DebtToIncomeRecord_CalculatesNetIncomeAffordabilityAndStressStatus()
    {
        var record = new DebtToIncomeRecord
        {
            GrossMonthlyIncome = 6000m,
            NetMonthlyIncome = 4500m,
            HousingMonthlyPayment = 1500m,
            OtherMonthlyDebtPayment = 500m,
            OtherEssentialMonthlyExpenses = 2200m
        };

        Assert.Equal(2000m, record.TotalMonthlyDebtObligations);
        Assert.Equal(44.444444444444444444444444440m, record.DebtPaymentToNetIncomePercent);
        Assert.Equal(300m, record.MonthlyIncomeAfterObligations);
        Assert.Equal(300m, record.AvailableForAdditionalDebtPayment);
        Assert.Equal("Limited flexibility", record.AffordabilityStatus);
    }

    [Fact]
    public void DebtToIncomeRecord_DistinguishesNegativeAndMissingNetIncome()
    {
        var missing = new DebtToIncomeRecord { GrossMonthlyIncome = 5000m };
        Assert.Equal("Net income not recorded", missing.AffordabilityStatus);
        Assert.Equal(0m, missing.AvailableForAdditionalDebtPayment);

        var negative = new DebtToIncomeRecord
        {
            NetMonthlyIncome = 3000m,
            HousingMonthlyPayment = 1800m,
            OtherMonthlyDebtPayment = 700m,
            OtherEssentialMonthlyExpenses = 800m
        };
        Assert.Equal(-300m, negative.MonthlyIncomeAfterObligations);
        Assert.Equal(0m, negative.AvailableForAdditionalDebtPayment);
        Assert.Equal("Expenses exceed net income", negative.AffordabilityStatus);
    }

    [Theory]
    [InlineData(5, "Below 10%")]
    [InlineData(10, "Below 30%")]
    [InlineData(30, "30–49.9%")]
    [InlineData(50, "50–74.9%")]
    [InlineData(75, "75–89.9%")]
    [InlineData(90, "90% or higher")]
    public void CreditImpact_MapsProjectedUtilizationToIndicator(double utilization, string expected)
    {
        var result = new CreditScoreImpactResult { ProjectedUtilizationPercent = (decimal)utilization };
        Assert.Equal(expected, result.UtilizationIndicator);
    }

    [Fact]
    public void TaxInterestReport_CalculatesNetInterestCost()
    {
        var report = new DebtTaxInterestReport
        {
            InterestCharged = 325.50m,
            InterestEarned = 25.50m
        };

        Assert.Equal(300m, report.NetInterestCost);
    }

}
