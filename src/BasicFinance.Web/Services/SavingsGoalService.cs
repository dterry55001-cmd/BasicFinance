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
