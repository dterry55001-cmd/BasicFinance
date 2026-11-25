namespace BasicFinance.Web.Services;

public static class BudgetRolloverCalculator
{
    public static decimal CalculateNextMonth(decimal plannedAmount, decimal currentRollover, decimal actualSpending, bool rolloverEnabled)
    {
        if (plannedAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(plannedAmount), "Planned budget cannot be negative.");
        if (actualSpending < 0m)
            throw new ArgumentOutOfRangeException(nameof(actualSpending), "Actual spending cannot be negative.");

        return rolloverEnabled ? plannedAmount + currentRollover - actualSpending : 0m;
    }
}
