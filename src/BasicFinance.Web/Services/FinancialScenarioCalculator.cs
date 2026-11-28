using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

/// <summary>
/// Runs read-only what-if projections against caller-supplied baseline figures.
/// No database, account, budget, debt, or transaction records are modified.
/// </summary>
public sealed class FinancialScenarioCalculator
{
    public FinancialScenarioComparison Compare(
        FinancialScenarioInput input,
        FinancialScenarioAssumptions scenarioAssumptions,
        IReadOnlyList<FinancialScenarioEvent>? scenarioEvents = null,
        string scenarioName = "What-if scenario")
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(scenarioAssumptions);
        Validate(input);
        ValidateAssumptions(scenarioAssumptions);
        ValidateEvents(scenarioEvents, input.ProjectionMonths);
        var effectiveScenarioRate = input.AnnualDebtRatePercent + scenarioAssumptions.AnnualDebtRateChange;
        if (effectiveScenarioRate is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(scenarioAssumptions), "The resulting annual debt rate must be between 0 and 100 percent.");
        var baseline = Run(input, input.Assumptions, input.Events, "Baseline");
        var scenario = Run(input, scenarioAssumptions, scenarioEvents, scenarioName);
        return new FinancialScenarioComparison(
            baseline, scenario,
            decimal.Round(scenario.EndingDebtBalance - baseline.EndingDebtBalance, 2),
            decimal.Round(scenario.EndingSavingsBalance - baseline.EndingSavingsBalance, 2),
            decimal.Round(scenario.CumulativeNetCashFlow - baseline.CumulativeNetCashFlow, 2));
    }

    private static FinancialScenarioResult Run(
        FinancialScenarioInput input,
        FinancialScenarioAssumptions assumptions,
        IReadOnlyList<FinancialScenarioEvent>? events,
        string name)
    {
        var eventMap = (events ?? []).GroupBy(e => e.Month).ToDictionary(g => g.Key, g => g.ToArray());
        var debt = input.StartingDebtBalance;
        var savings = input.StartingSavingsBalance;
        var totalIncome = 0m;
        var totalExpenses = 0m;
        var totalInterest = 0m;
        var totalDebtPayments = 0m;
        var cumulativeCashFlow = 0m;
        int? debtFreeMonth = debt <= 0m ? 0 : null;
        var rows = new List<FinancialScenarioMonth>(input.ProjectionMonths);

        for (var month = 1; month <= input.ProjectionMonths; month++)
        {
            eventMap.TryGetValue(month, out var monthEvents);
            var eventIncome = monthEvents?.Sum(e => e.IncomeChange) ?? 0m;
            var eventExpenses = monthEvents?.Sum(e => e.ExpenseChange) ?? 0m;
            var eventDebtPayment = monthEvents?.Sum(e => e.DebtPaymentChange) ?? 0m;
            var eventSavings = monthEvents?.Sum(e => e.SavingsContributionChange) ?? 0m;

            var income = input.MonthlyIncome + assumptions.MonthlyIncomeChange + eventIncome;
            var expenses = input.MonthlyExpenses + assumptions.MonthlyExpenseChange + eventExpenses;
            var rate = input.AnnualDebtRatePercent + assumptions.AnnualDebtRateChange;
            var debtInterest = debt > 0m
                ? decimal.Round(debt * rate / 1200m, 2, MidpointRounding.AwayFromZero)
                : 0m;
            var debtPaymentBudget = Math.Max(0m,
                input.MonthlyDebtPayment + assumptions.MonthlyDebtPaymentChange + eventDebtPayment);
            var debtPayment = Math.Min(debt + debtInterest, debtPaymentBudget);
            debt = Math.Max(0m, decimal.Round(debt + debtInterest - debtPayment, 2, MidpointRounding.AwayFromZero));
            if (debt <= 0m && !debtFreeMonth.HasValue) debtFreeMonth = month;

            var savingsContribution = Math.Max(0m,
                input.MonthlySavingsContribution + assumptions.MonthlySavingsContributionChange + eventSavings);
            savings = Math.Max(0m, decimal.Round(savings + savingsContribution, 2, MidpointRounding.AwayFromZero));
            var netCashFlow = income - expenses - debtPayment - savingsContribution;

            totalIncome += income;
            totalExpenses += expenses;
            totalInterest += debtInterest;
            totalDebtPayments += debtPayment;
            cumulativeCashFlow += netCashFlow;
            rows.Add(new FinancialScenarioMonth(
                month, income, expenses, debtInterest, debtPayment, debt,
                savingsContribution, savings, netCashFlow));
        }

        return new FinancialScenarioResult(
            name, totalIncome, totalExpenses, totalInterest, totalDebtPayments,
            debt, savings, cumulativeCashFlow, debtFreeMonth, rows);
    }

    private static void Validate(FinancialScenarioInput input)
    {
        if (input.StartingDebtBalance < 0m || input.StartingSavingsBalance < 0m ||
            input.MonthlyDebtPayment < 0m || input.MonthlySavingsContribution < 0m ||
            input.MonthlyIncome < 0m || input.MonthlyExpenses < 0m)
            throw new ArgumentOutOfRangeException(nameof(input), "Starting balances, payment amounts, income, and expenses cannot be negative.");
        if (input.AnnualDebtRatePercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(input.AnnualDebtRatePercent));
        if (input.ProjectionMonths is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(input.ProjectionMonths));
        ValidateAssumptions(input.Assumptions);
        ValidateEvents(input.Events, input.ProjectionMonths);
    }

    private static void ValidateAssumptions(FinancialScenarioAssumptions assumptions)
    {
        if (assumptions.AnnualDebtRateChange is < -100m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(assumptions.AnnualDebtRateChange));
        if (assumptions.AnnualDebtRateChange < -100m)
            throw new ArgumentOutOfRangeException(nameof(assumptions.AnnualDebtRateChange));
    }

    private static void ValidateEvents(IReadOnlyList<FinancialScenarioEvent>? events, int months)
    {
        if (events is null) return;
        if (events.Any(e => e.Month < 1 || e.Month > months))
            throw new ArgumentOutOfRangeException(nameof(events), "Scenario event months must fall within the projection horizon.");
    }
}
