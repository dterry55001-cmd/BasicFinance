using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

public sealed class FinancialPlanningDashboardService(IFinancialPlanningDataProvider dataProvider)
{
    public async Task<FinancialPlanningDashboardViewModel> BuildAsync(
        DateOnly asOfDate, string reportingCurrency, CancellationToken cancellationToken = default)
    {
        var data = await dataProvider.GetPlanningDataAsync(asOfDate, reportingCurrency, cancellationToken);
        var summaries = data.Accounts
            .GroupBy(x => x.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key)
            .Select(group =>
            {
                var assets = group.Where(x => x.AccountType is not (AccountType.Debt or AccountType.CreditCard)).ToArray();
                var liabilities = group.Where(x => x.AccountType is AccountType.Debt or AccountType.CreditCard).ToArray();
                return new FinancialPlanningCurrencySummary(
                    group.Key,
                    assets.Sum(x => x.Balance),
                    liabilities.Sum(x => Math.Max(0m, x.Balance)),
                    assets.Sum(x => x.Balance) - liabilities.Sum(x => Math.Max(0m, x.Balance)),
                    assets.Where(x => x.AccountType is AccountType.Checking or AccountType.Savings or AccountType.Cash).Sum(x => x.Balance),
                    assets.Where(x => x.AccountType == AccountType.Retirement).Sum(x => x.Balance),
                    assets.Length,
                    liabilities.Length);
            }).ToArray();

        var matchingBudgets = data.Budgets.Where(x => x.CurrencyCode == reportingCurrency).ToArray();
        var matchingGoals = data.SavingsGoals.Where(x => x.CurrencyCode == reportingCurrency).ToArray();
        return new FinancialPlanningDashboardViewModel
        {
            AsOfDate = asOfDate,
            ReportingCurrency = reportingCurrency,
            CurrencySummaries = summaries,
            Sources = data.Sources,
            Warnings = data.Warnings,
            BudgetLineCount = matchingBudgets.Length,
            PlannedBudgetTotal = matchingBudgets.Sum(x => x.PlannedAmount + x.RolloverAmount),
            SavingsGoalCount = matchingGoals.Length,
            SavingsGoalProgressTotal = matchingGoals.Sum(x => x.CurrentAmount),
            SavingsGoalTargetTotal = matchingGoals.Sum(x => x.TargetAmount),
            DebtAccountsMissingDetails = data.Debts.Count(x => x.AnnualRatePercent is null || x.MinimumPayment is null)
        };
    }
}
