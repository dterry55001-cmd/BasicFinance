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

    [Theory]
    [InlineData(TransactionType.Income, SavingsContributionType.Contribution)]
    [InlineData(TransactionType.Deposit, SavingsContributionType.Contribution)]
    [InlineData(TransactionType.Expense, SavingsContributionType.Withdrawal)]
    [InlineData(TransactionType.Withdrawal, SavingsContributionType.Withdrawal)]
    public void IsCompatibleTransaction_AcceptsMatchingTransactionTypes(TransactionType transactionType, SavingsContributionType contributionType)
    {
        var transaction = new FinancialTransaction
        {
            Type = transactionType,
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 11, 20)
        };

        Assert.True(_service.IsCompatibleTransaction(transaction, contributionType, 125m,
            new DateTime(2026, 11, 20), "USD"));
    }

    [Fact]
    public void IsCompatibleTransaction_RejectsMismatchedAmountDateCurrencyAndType()
    {
        var transaction = new FinancialTransaction
        {
            Type = TransactionType.Expense,
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 11, 20)
        };

        Assert.False(_service.IsCompatibleTransaction(transaction, SavingsContributionType.Contribution, 125m,
            new DateTime(2026, 11, 20), "USD"));
        Assert.False(_service.IsCompatibleTransaction(transaction, SavingsContributionType.Withdrawal, 124m,
            new DateTime(2026, 11, 20), "USD"));
        Assert.False(_service.IsCompatibleTransaction(transaction, SavingsContributionType.Withdrawal, 125m,
            new DateTime(2026, 11, 21), "USD"));
        Assert.False(_service.IsCompatibleTransaction(transaction, SavingsContributionType.Withdrawal, 125m,
            new DateTime(2026, 11, 20), "EUR"));
        Assert.False(_service.IsCompatibleTransaction(null, SavingsContributionType.Withdrawal, 125m,
            new DateTime(2026, 11, 20), "USD"));
    }

    [Theory]
    [InlineData(SavingsScheduleFrequency.Weekly, 7)]
    [InlineData(SavingsScheduleFrequency.Biweekly, 14)]
    [InlineData(SavingsScheduleFrequency.Monthly, 31)]
    [InlineData(SavingsScheduleFrequency.Quarterly, 90)]
    [InlineData(SavingsScheduleFrequency.Yearly, 365)]
    public void GetNextSavingsDueDate_AdvancesByConfiguredFrequency(SavingsScheduleFrequency frequency, int expectedDays)
    {
        var dueDate = new DateTime(2026, 1, 1);
        var result = _service.GetNextSavingsDueDate(dueDate, frequency);

        Assert.Equal(dueDate.AddDays(expectedDays), result);
    }

    [Fact]
    public void CalculateRequiredMonthlyContribution_UsesMonthsUntilTargetAndRoundsUp()
    {
        var today = new DateTime(2026, 11, 25);

        Assert.Equal(150m, _service.CalculateRequiredMonthlyContribution(300m, new DateTime(2027, 1, 25), today));
        Assert.Equal(300m, _service.CalculateRequiredMonthlyContribution(300m, new DateTime(2026, 12, 1), today));
        Assert.Equal(300m, _service.CalculateRequiredMonthlyContribution(300m, new DateTime(2026, 10, 1), today));
        Assert.Null(_service.CalculateRequiredMonthlyContribution(300m, null, today));
        Assert.Equal(0m, _service.CalculateRequiredMonthlyContribution(0m, new DateTime(2027, 1, 1), today));
    }

    [Fact]
    public void CalculateAllocationAmount_RoundsToCents()
    {
        Assert.Equal(333.33m, _service.CalculateAllocationAmount(1000m, 33.333m));
    }

    [Fact]
    public void IsAllocationTotalValid_PreventsAllocationsAboveOneHundredPercent()
    {
        Assert.True(_service.IsAllocationTotalValid(new[] { 30m, 20m }, 50m));
        Assert.False(_service.IsAllocationTotalValid(new[] { 30m, 20m }, 50.01m));
        Assert.False(_service.IsAllocationTotalValid(new[] { 30m }, 100.01m));
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
