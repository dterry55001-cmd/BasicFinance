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


    [Fact]
    public void IsCompatibleTransaction_ValidatesTransferDirectionAgainstLinkedAccount()
    {
        var incomingTransfer = new FinancialTransaction
        {
            Type = TransactionType.Transfer,
            AccountId = 10,
            RelatedAccountId = 20,
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 11, 20)
        };
        var outgoingTransfer = new FinancialTransaction
        {
            Type = TransactionType.Transfer,
            AccountId = 20,
            RelatedAccountId = 10,
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 11, 20)
        };

        Assert.True(_service.IsCompatibleTransaction(incomingTransfer, SavingsContributionType.Contribution,
            125m, new DateTime(2026, 11, 20), "USD", 20));
        Assert.False(_service.IsCompatibleTransaction(incomingTransfer, SavingsContributionType.Contribution,
            125m, new DateTime(2026, 11, 20), "USD", 10));
        Assert.True(_service.IsCompatibleTransaction(outgoingTransfer, SavingsContributionType.Withdrawal,
            125m, new DateTime(2026, 11, 20), "USD", 20));
        Assert.False(_service.IsCompatibleTransaction(outgoingTransfer, SavingsContributionType.Withdrawal,
            125m, new DateTime(2026, 11, 20), "USD", 10));
        Assert.False(_service.IsCompatibleTransaction(incomingTransfer, SavingsContributionType.Contribution,
            125m, new DateTime(2026, 11, 20), "USD"));
    }

    [Fact]
    public void IsCompatibleTransaction_RequiresNonTransferActivityToAffectLinkedAccount()
    {
        var income = new FinancialTransaction
        {
            Type = TransactionType.Income,
            AccountId = 20,
            Amount = 125m,
            CurrencyCode = "USD",
            TransactionDate = new DateTime(2026, 11, 20)
        };

        Assert.True(_service.IsCompatibleTransaction(income, SavingsContributionType.Contribution,
            125m, new DateTime(2026, 11, 20), "USD", 20));
        Assert.False(_service.IsCompatibleTransaction(income, SavingsContributionType.Contribution,
            125m, new DateTime(2026, 11, 20), "USD", 10));
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
    public void BuildEmergencyFundProjection_ProjectsRecurringMonthlyContributionsWithoutCreatingEntries()
    {
        var schedules = new[]
        {
            new RecurringSavingsContribution
            {
                Amount = 500m,
                Frequency = SavingsScheduleFrequency.Monthly,
                NextDueDate = new DateTime(2026, 12, 5),
                IsActive = true
            }
        };

        var result = _service.BuildEmergencyFundProjection(
            1000m, 1000m, 3, schedules, new DateTime(2026, 11, 27), 6);

        Assert.Equal(3000m, result.TargetAmount);
        Assert.Equal(new DateTime(2027, 3, 31), result.EstimatedCompletionDate);
        Assert.Equal(new[] { 500m, 500m, 500m, 500m, 500m, 500m },
            result.Months.Select(month => month.PlannedContributions));
        Assert.Equal(4000m, result.Months[^1].ProjectedClosingBalance);
        Assert.Empty(schedules[0].SavingsGoal?.Contributions ?? []);
    }

    [Fact]
    public void BuildEmergencyFundProjection_RespectsScheduleEndDate()
    {
        var schedule = new RecurringSavingsContribution
        {
            Amount = 250m,
            Frequency = SavingsScheduleFrequency.Monthly,
            NextDueDate = new DateTime(2026, 12, 1),
            EndDate = new DateTime(2027, 1, 31),
            IsActive = true
        };

        var result = _service.BuildEmergencyFundProjection(
            0m, 1000m, 1, [schedule], new DateTime(2026, 11, 27), 4);

        Assert.Equal(new[] { 250m, 250m, 0m, 0m },
            result.Months.Select(month => month.PlannedContributions));
        Assert.Null(result.EstimatedCompletionDate);
    }

    [Fact]
    public void BuildEmergencyFundProjection_ReportsAlreadyFundedAndNoContributionScenarios()
    {
        var alreadyFunded = _service.BuildEmergencyFundProjection(
            6000m, 1000m, 6, [], new DateTime(2026, 11, 27), 3);
        var noSchedule = _service.BuildEmergencyFundProjection(
            100m, 1000m, 6, [], new DateTime(2026, 11, 27), 3);

        Assert.Equal(new DateTime(2026, 11, 27), alreadyFunded.EstimatedCompletionDate);
        Assert.Null(noSchedule.EstimatedCompletionDate);
        Assert.All(noSchedule.Months, month => Assert.Equal(100m, month.ProjectedClosingBalance));
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
    [Theory]
    [InlineData(2025, 1, 31, 2025, 2, 28)]
    [InlineData(2024, 1, 31, 2024, 2, 29)]
    [InlineData(2024, 2, 29, 2025, 2, 28)]
    [InlineData(2024, 8, 31, 2024, 9, 30)]
    public void GetNextSavingsDueDate_MonthlyAndYearlySchedulesClampToValidCalendarDay(
        int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        var frequency = month == 2 && day == 29 ? SavingsScheduleFrequency.Yearly : SavingsScheduleFrequency.Monthly;
        var result = _service.GetNextSavingsDueDate(new DateTime(year, month, day), frequency);

        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), result);
    }

    [Fact]
    public void GetNextSavingsDueDate_RejectsUndefinedFrequency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.GetNextSavingsDueDate(new DateTime(2026, 11, 26), (SavingsScheduleFrequency)999));
    }

    [Theory]
    [InlineData(0, 1500, 0)]
    [InlineData(-500, 1500, 0)]
    [InlineData(4500, 1500, 3)]
    [InlineData(10000, 2500, 4)]
    public void CalculateEmergencyFundCoverageMonths_HandlesZeroAndNegativeBalances(
        decimal currentAmount, decimal monthlyExpenses, decimal expectedMonths)
    {
        Assert.Equal(expectedMonths,
            _service.CalculateEmergencyFundCoverageMonths(currentAmount, monthlyExpenses));
    }

    [Fact]
    public void CalculateRequiredMonthlyContribution_RoundsUpToNearestCent()
    {
        var result = _service.CalculateRequiredMonthlyContribution(
            100m, new DateTime(2027, 2, 1), new DateTime(2026, 11, 1));

        Assert.Equal(33.34m, result);
    }

    [Fact]
    public void BuildEmergencyFundReadiness_CalculatesOneThreeSixAndTwelveMonthGaps()
    {
        var result = _service.BuildEmergencyFundReadiness(3000m, 1500m);

        Assert.Equal(new[] { 1, 3, 6, 12 }, result.Select(x => x.CoverageMonths));
        Assert.Equal(new[] { 1500m, 4500m, 9000m, 18000m }, result.Select(x => x.TargetAmount));
        Assert.Equal(new[] { 0m, 1500m, 6000m, 15000m }, result.Select(x => x.FundingGap));
        Assert.Equal(new[] { 0m, 125m, 500m, 1250m }, result.Select(x => x.SuggestedMonthlyContribution));
        Assert.Equal(new[] { true, false, false, false }, result.Select(x => x.IsFunded));
    }

    [Fact]
    public void BuildEmergencyFundReadiness_HandlesNoExpensesAndNegativeBalance()
    {
        var noExpenses = _service.BuildEmergencyFundReadiness(500m, 0m);
        Assert.All(noExpenses, tier => Assert.True(tier.IsFunded));
        Assert.All(noExpenses, tier => Assert.Equal(0m, tier.TargetAmount));

        var negativeBalance = _service.BuildEmergencyFundReadiness(-100m, 1000m);
        Assert.All(negativeBalance, tier => Assert.Equal(tier.TargetAmount, tier.FundingGap));
    }

}
