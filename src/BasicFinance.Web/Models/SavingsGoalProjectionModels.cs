namespace BasicFinance.Web.Models;

public enum SavingsProjectionEventType
{
    Contribution = 1,
    Withdrawal = 2
}

public sealed record PlannedSavingsProjectionEvent(
    DateTime EventDate,
    decimal Amount,
    SavingsProjectionEventType Type,
    string? Description = null);

public sealed record SavingsGoalProjectionMonth(
    DateTime Month,
    decimal OpeningBalance,
    decimal PlannedContributions,
    decimal PlannedWithdrawals,
    decimal ClosingBalance,
    decimal TargetProgressPercent,
    decimal RemainingAmount,
    bool TargetReached);

public sealed record SavingsGoalProjectionResult(
    decimal StartingBalance,
    decimal TargetAmount,
    DateTime? EstimatedCompletionDate,
    string CurrencyCode,
    IReadOnlyList<SavingsGoalProjectionMonth> Months);
