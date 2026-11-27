using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

public sealed class SavingsGoalService
{
    public SavingsGoalProgress CalculateProgress(SavingsGoal goal, DateTime today)
    {
        var entries = goal.Contributions
            .Where(x => x.ContributionDate.Date <= today.Date)
            .ToList();
        var currentAmount = entries.Sum(x => x.Type == SavingsContributionType.Contribution ? x.Amount : -x.Amount);
        var remainingAmount = Math.Max(0m, goal.TargetAmount - currentAmount);
        var progressPercent = goal.TargetAmount <= 0m
            ? 0m
            : Math.Clamp(currentAmount / goal.TargetAmount * 100m, 0m, 100m);

        var threeMonthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
        var recentNetContributions = entries
            .Where(x => x.ContributionDate.Date >= threeMonthStart)
            .Sum(x => x.Type == SavingsContributionType.Contribution ? x.Amount : -x.Amount);
        var averageMonthlyContribution = recentNetContributions / 3m;
        DateTime? estimatedCompletionDate = remainingAmount == 0m
            ? today.Date
            : averageMonthlyContribution > 0m
                ? today.Date.AddMonths((int)Math.Ceiling(remainingAmount / averageMonthlyContribution))
                : null;

        return new SavingsGoalProgress(currentAmount, remainingAmount, progressPercent, averageMonthlyContribution, estimatedCompletionDate);
    }

    public bool IsCompatibleTransaction(FinancialTransaction? transaction, SavingsContributionType contributionType,
        decimal amount, DateTime contributionDate, string currencyCode, int? linkedAccountId = null)
    {
        if (transaction is null || amount <= 0m) return false;

        var compatibleType = transaction.Type == TransactionType.Transfer
            || (contributionType == SavingsContributionType.Contribution
                ? transaction.Type is TransactionType.Income or TransactionType.Deposit
                : contributionType == SavingsContributionType.Withdrawal
                    && transaction.Type is TransactionType.Expense or TransactionType.Withdrawal);

        return compatibleType
            && string.Equals(transaction.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase)
            && transaction.Amount == amount
            && transaction.TransactionDate.Date == contributionDate.Date
            && IsTransactionForLinkedAccount(transaction, contributionType, linkedAccountId);
    }


    public bool IsTransactionForLinkedAccount(
        FinancialTransaction transaction,
        SavingsContributionType contributionType,
        int? linkedAccountId)
    {
        if (!linkedAccountId.HasValue) return transaction.Type != TransactionType.Transfer;
        if (transaction.Type == TransactionType.Transfer)
        {
            return contributionType == SavingsContributionType.Contribution
                ? transaction.RelatedAccountId == linkedAccountId.Value
                : transaction.AccountId == linkedAccountId.Value;
        }

        if (transaction.AccountId != linkedAccountId.Value) return false;
        return contributionType == SavingsContributionType.Contribution
            ? transaction.Type is TransactionType.Income or TransactionType.Deposit
            : transaction.Type is TransactionType.Expense or TransactionType.Withdrawal;
    }

    public DateTime GetNextSavingsDueDate(DateTime currentDueDate, SavingsScheduleFrequency frequency)
    {
        return frequency switch
        {
            SavingsScheduleFrequency.Weekly => currentDueDate.Date.AddDays(7),
            SavingsScheduleFrequency.Biweekly => currentDueDate.Date.AddDays(14),
            SavingsScheduleFrequency.Monthly => currentDueDate.Date.AddMonths(1),
            SavingsScheduleFrequency.Quarterly => currentDueDate.Date.AddMonths(3),
            SavingsScheduleFrequency.Yearly => currentDueDate.Date.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
    }

    public decimal? CalculateRequiredMonthlyContribution(decimal remainingAmount, DateTime? targetDate, DateTime today)
    {
        if (!targetDate.HasValue) return null;
        if (remainingAmount <= 0m) return 0m;

        var target = targetDate.Value.Date;
        var months = (target.Year - today.Year) * 12 + target.Month - today.Month;
        if (target.Day < today.Day) months--;
        months = Math.Max(1, months);

        return decimal.Ceiling(remainingAmount / months * 100m) / 100m;
    }


    public SavingsCashFlowCapacity CalculateCashFlowCapacity(
        IEnumerable<FinancialTransaction> transactions,
        IEnumerable<RecurringSavingsContribution> recurringSchedules,
        DateTime today,
        decimal? userMonthlyBudget)
    {
        if (userMonthlyBudget < 0m) throw new ArgumentOutOfRangeException(nameof(userMonthlyBudget));

        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var periodStart = currentMonth.AddMonths(-3);
        var eligibleTransactions = transactions
            .Where(x => x.CurrencyCode == "USD" && x.TransactionDate.Date >= periodStart
                && x.TransactionDate.Date < currentMonth)
            .ToList();

        var monthlyIncome = eligibleTransactions
            .Where(x => x.Type is TransactionType.Income or TransactionType.Deposit)
            .Sum(x => x.Amount) / 3m;
        var monthlyExpenses = eligibleTransactions
            .Where(x => x.Type is TransactionType.Expense or TransactionType.Withdrawal)
            .Sum(x => x.Amount) / 3m;
        var averageMonthlySurplus = monthlyIncome - monthlyExpenses;

        var committedMonthlySavings = recurringSchedules
            .Where(x => x.IsActive && (!x.EndDate.HasValue || x.NextDueDate.Date <= x.EndDate.Value.Date))
            .Sum(x => x.Frequency switch
            {
                SavingsScheduleFrequency.Weekly => x.Amount * 52m / 12m,
                SavingsScheduleFrequency.Biweekly => x.Amount * 26m / 12m,
                SavingsScheduleFrequency.Monthly => x.Amount,
                SavingsScheduleFrequency.Quarterly => x.Amount / 3m,
                SavingsScheduleFrequency.Yearly => x.Amount / 12m,
                _ => 0m
            });

        var surplusAfterCommitments = Math.Max(0m, averageMonthlySurplus - committedMonthlySavings);
        var recommendedCapacity = userMonthlyBudget.HasValue
            ? Math.Min(surplusAfterCommitments, userMonthlyBudget.Value)
            : surplusAfterCommitments;

        return new SavingsCashFlowCapacity(
            periodStart, currentMonth.AddDays(-1), monthlyIncome, monthlyExpenses,
            averageMonthlySurplus, committedMonthlySavings, surplusAfterCommitments,
            recommendedCapacity, userMonthlyBudget.HasValue);
    }

    public decimal CalculateAllocationAmount(decimal monthlyBudget, decimal allocationPercent)
    {
        if (monthlyBudget < 0m || allocationPercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(allocationPercent));
        return decimal.Round(monthlyBudget * allocationPercent / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public bool IsAllocationTotalValid(IEnumerable<decimal> percentages, decimal proposedPercent)
    {
        if (proposedPercent is < 0m or > 100m) return false;
        return percentages.Sum() + proposedPercent <= 100m;
    }

    public decimal CalculateEmergencyFundCoverageMonths(decimal currentAmount, decimal monthlyEssentialExpenses)
    {
        if (monthlyEssentialExpenses <= 0m) return 0m;
        return Math.Max(0m, currentAmount) / monthlyEssentialExpenses;
    }

    public decimal CalculateMonthlyNetContribution(IEnumerable<SavingsContribution> contributions, int year, int month)
    {
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        return contributions
            .Where(x => x.ContributionDate >= start && x.ContributionDate < end)
            .Sum(x => x.Type == SavingsContributionType.Contribution ? x.Amount : -x.Amount);
    }
}

public sealed record SavingsGoalProgress(
    decimal CurrentAmount,
    decimal RemainingAmount,
    decimal ProgressPercent,
    decimal AverageMonthlyContribution,
    DateTime? EstimatedCompletionDate);


public sealed record SavingsCashFlowCapacity(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal AverageMonthlyIncome,
    decimal AverageMonthlyExpenses,
    decimal AverageMonthlySurplus,
    decimal ExistingMonthlySavingsCommitments,
    decimal SurplusAfterCommitments,
    decimal RecommendedMonthlyCapacity,
    bool UsesUserBudget);
