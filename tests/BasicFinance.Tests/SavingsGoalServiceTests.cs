using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class SavingsGoalServiceTests
{
    private readonly SavingsGoalService _service = new();

    [Fact]
    public void CalculateProgress_NetsWithdrawalsAndExcludesFutureEntries()
    {
        var goal = new SavingsGoal
        {
            TargetAmount = 1000m,
            Contributions =
            [
                new SavingsContribution { Amount = 200m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 9, 20) },
                new SavingsContribution { Amount = 300m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 10, 20) },
                new SavingsContribution { Amount = 100m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 11, 20) },
                new SavingsContribution { Amount = 50m, Type = SavingsContributionType.Withdrawal, ContributionDate = new DateTime(2026, 11, 22) },
                new SavingsContribution { Amount = 1000m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 11, 26) }
            ]
        };

        var result = _service.CalculateProgress(goal, new DateTime(2026, 11, 25));

        Assert.Equal(550m, result.CurrentAmount);
        Assert.Equal(450m, result.RemainingAmount);
        Assert.Equal(55m, result.ProgressPercent);
        Assert.Equal(183.33333333333333333333333333m, result.AverageMonthlyContribution);
        Assert.Equal(new DateTime(2027, 2, 25), result.EstimatedCompletionDate);
    }

    [Fact]
    public void CalculateProgress_ReturnsNoEstimateWithoutPositiveRecentSavings()
    {
        var goal = new SavingsGoal { TargetAmount = 2000m };

        var result = _service.CalculateProgress(goal, new DateTime(2026, 11, 25));

        Assert.Equal(0m, result.CurrentAmount);
        Assert.Null(result.EstimatedCompletionDate);
    }

    [Fact]
    public void CalculateEmergencyFundCoverageMonths_UsesMonthlyEssentialExpenses()
    {
        Assert.Equal(3m, _service.CalculateEmergencyFundCoverageMonths(4500m, 1500m));
        Assert.Equal(0m, _service.CalculateEmergencyFundCoverageMonths(4500m, 0m));
    }

    [Fact]
    public void CalculateMonthlyNetContribution_SubtractsWithdrawalsWithinSelectedMonth()
    {
        var entries = new[]
        {
            new SavingsContribution { Amount = 500m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 10, 31) },
            new SavingsContribution { Amount = 300m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 11, 2) },
            new SavingsContribution { Amount = 50m, Type = SavingsContributionType.Withdrawal, ContributionDate = new DateTime(2026, 11, 10) },
            new SavingsContribution { Amount = 200m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 12, 1) }
        };

        var result = _service.CalculateMonthlyNetContribution(entries, 2026, 11);

        Assert.Equal(250m, result);
    }
}
