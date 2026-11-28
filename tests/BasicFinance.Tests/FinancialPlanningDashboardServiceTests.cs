using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Xunit;

namespace BasicFinance.Tests;

public sealed class FinancialPlanningDashboardServiceTests
{
    [Fact]
    public async Task BuildAsync_CalculatesNetWorthPerCurrencyAndDoesNotAddSavingsProgressToAssets()
    {
        var data = CreateData(
            accounts:
            [
                Account(1, AccountType.Checking, "USD", 1000m),
                Account(2, AccountType.Brokerage, "USD", 500m),
                Account(3, AccountType.Debt, "USD", 2200m),
                Account(4, AccountType.Retirement, "EUR", 8000m)
            ],
            savings:
            [
                new PlanningSavingsGoalInput(10, "Emergency fund", "USD", 5000m, 3000m,
                    new DateTime(2027, 1, 1), true, 1)
            ]);

        var service = new FinancialPlanningDashboardService(new StubProvider(data));
        var result = await service.BuildAsync(new DateOnly(2026, 11, 27), "USD");

        Assert.Equal(2, result.CurrencySummaries.Count);
        var usd = Assert.Single(result.CurrencySummaries.Where(x => x.CurrencyCode == "USD"));
        Assert.Equal(1500m, usd.TotalAssets);
        Assert.Equal(2200m, usd.TotalLiabilities);
        Assert.Equal(-700m, usd.NetWorth);
        Assert.Equal(1000m, usd.AvailableCash);
        Assert.Equal(0m, usd.RetirementBalances);

        var eur = Assert.Single(result.CurrencySummaries.Where(x => x.CurrencyCode == "EUR"));
        Assert.Equal(8000m, eur.TotalAssets);
        Assert.Equal(8000m, eur.NetWorth);
        Assert.Equal(8000m, eur.RetirementBalances);

        Assert.Equal(3000m, result.SavingsGoalProgressTotal);
        Assert.Equal(5000m, result.SavingsGoalTargetTotal);
        Assert.Equal(1500m, result.CurrencySummaries.Sum(x => x.CurrencyCode == "USD" ? x.TotalAssets : 0m));
    }

    [Fact]
    public async Task BuildAsync_ClampsNegativeLiabilityBalancesAndCountsMissingDebtDetails()
    {
        var data = CreateData(
            accounts:
            [
                Account(1, AccountType.Checking, "USD", 250m),
                Account(2, AccountType.CreditCard, "USD", -75m)
            ],
            debts:
            [
                new PlanningDebtInput(2, "Card", DebtAccountType.CreditCard, "USD", -75m, null, null, null)
            ]);

        var result = await new FinancialPlanningDashboardService(new StubProvider(data))
            .BuildAsync(new DateOnly(2026, 11, 27), "USD");

        var summary = Assert.Single(result.CurrencySummaries);
        Assert.Equal(250m, summary.TotalAssets);
        Assert.Equal(0m, summary.TotalLiabilities);
        Assert.Equal(250m, summary.NetWorth);
        Assert.Equal(1, result.DebtAccountsMissingDetails);
    }

    [Fact]
    public async Task BuildAsync_PreservesSourceWarningsAndExcludesOtherCurrencyBudgetAndGoalTotals()
    {
        var data = CreateData(
            accounts: [Account(1, AccountType.Checking, "USD", 100m)],
            budgets:
            [
                new PlanningBudgetInput(1, 2026, 11, "USD", 1, "Food", 400m, 25m),
                new PlanningBudgetInput(2, 2026, 11, "EUR", 2, "Travel", 700m, 0m)
            ],
            savings:
            [
                new PlanningSavingsGoalInput(1, "USD goal", "USD", 1000m, 200m, null, false, null),
                new PlanningSavingsGoalInput(2, "EUR goal", "EUR", 2000m, 500m, null, false, null)
            ],
            warnings: ["FX data unavailable"],
            sources: [new PlanningSourceMetadata("Budget Management", PlanningSourceStatus.Partial, DateTime.UtcNow, "Mixed currencies")]);

        var result = await new FinancialPlanningDashboardService(new StubProvider(data))
            .BuildAsync(new DateOnly(2026, 11, 27), "USD");

        Assert.Equal(1, result.BudgetLineCount);
        Assert.Equal(425m, result.PlannedBudgetTotal);
        Assert.Equal(1, result.SavingsGoalCount);
        Assert.Equal(200m, result.SavingsGoalProgressTotal);
        Assert.Contains("FX data unavailable", result.Warnings);
        Assert.Contains(result.Sources, x => x.Status == PlanningSourceStatus.Partial);
    }

    [Fact]
    public async Task BuildAsync_MatchesBudgetAndSavingsCurrencyCodesWithoutCaseSensitivity()
    {
        var data = CreateData(
            accounts: [Account(1, AccountType.Checking, "usd", 100m)],
            budgets:
            [
                new PlanningBudgetInput(1, 2026, 11, "usd", 1, "Food", 300m, 20m),
                new PlanningBudgetInput(2, 2026, 11, "EUR", 2, "Travel", 900m, 0m)
            ],
            savings:
            [
                new PlanningSavingsGoalInput(1, "Reserve", "uSd", 1000m, 250m, null, false, null),
                new PlanningSavingsGoalInput(2, "Foreign goal", "EUR", 2000m, 700m, null, false, null)
            ]);

        var result = await new FinancialPlanningDashboardService(new StubProvider(data))
            .BuildAsync(new DateOnly(2026, 11, 27), "USD");

        Assert.Equal(1, result.BudgetLineCount);
        Assert.Equal(320m, result.PlannedBudgetTotal);
        Assert.Equal(1, result.SavingsGoalCount);
        Assert.Equal(250m, result.SavingsGoalProgressTotal);
        Assert.Equal(1000m, result.SavingsGoalTargetTotal);
        Assert.Equal(100m, Assert.Single(result.CurrencySummaries).TotalAssets);
    }

    private static PlanningAccountInput Account(int id, AccountType type, string currency, decimal balance) =>
        new(id, $"Account {id}", type, currency, balance, null, null, null, null, null, null, null);

    private static FinancialPlanningData CreateData(
        IReadOnlyList<PlanningAccountInput> accounts,
        IReadOnlyList<PlanningDebtInput>? debts = null,
        IReadOnlyList<PlanningBudgetInput>? budgets = null,
        IReadOnlyList<PlanningSavingsGoalInput>? savings = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<PlanningSourceMetadata>? sources = null) =>
        new(new DateOnly(2026, 11, 27), "USD", accounts, debts ?? [], budgets ?? [], savings ?? [],
            [], [], sources ?? [], warnings ?? []);

    private sealed class StubProvider(FinancialPlanningData data) : IFinancialPlanningDataProvider
    {
        public Task<FinancialPlanningData> GetPlanningDataAsync(
            DateOnly asOfDate, string reportingCurrency, CancellationToken cancellationToken = default) =>
            Task.FromResult(data with { AsOfDate = asOfDate, RequestedReportingCurrency = reportingCurrency });
    }
}
