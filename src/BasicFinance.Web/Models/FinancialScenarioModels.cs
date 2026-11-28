namespace BasicFinance.Web.Models;

/// <summary>Changes are hypothetical deltas from a captured baseline; they never edit source records.</summary>
public sealed record FinancialScenarioAssumptions(
    decimal MonthlyIncomeChange = 0m,
    decimal MonthlyExpenseChange = 0m,
    decimal AnnualDebtRateChange = 0m,
    decimal MonthlyDebtPaymentChange = 0m,
    decimal MonthlySavingsContributionChange = 0m);

public sealed record FinancialScenarioEvent(
    int Month,
    decimal IncomeChange = 0m,
    decimal ExpenseChange = 0m,
    decimal DebtPaymentChange = 0m,
    decimal SavingsContributionChange = 0m);

public sealed record FinancialScenarioInput(
    decimal StartingDebtBalance,
    decimal AnnualDebtRatePercent,
    decimal MonthlyDebtPayment,
    decimal StartingSavingsBalance,
    decimal MonthlySavingsContribution,
    decimal MonthlyIncome,
    decimal MonthlyExpenses,
    int ProjectionMonths,
    FinancialScenarioAssumptions Assumptions,
    IReadOnlyList<FinancialScenarioEvent>? Events = null);

public sealed record FinancialScenarioMonth(
    int Month,
    decimal Income,
    decimal Expenses,
    decimal DebtInterest,
    decimal DebtPayment,
    decimal ClosingDebtBalance,
    decimal SavingsContribution,
    decimal ClosingSavingsBalance,
    decimal NetCashFlow);

public sealed record FinancialScenarioResult(
    string ScenarioName,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal TotalDebtInterest,
    decimal TotalDebtPayments,
    decimal EndingDebtBalance,
    decimal EndingSavingsBalance,
    decimal CumulativeNetCashFlow,
    int? DebtFreeMonth,
    IReadOnlyList<FinancialScenarioMonth> Months);

public sealed record FinancialScenarioComparison(
    FinancialScenarioResult Baseline,
    FinancialScenarioResult Scenario,
    decimal DifferenceInEndingDebt,
    decimal DifferenceInEndingSavings,
    decimal DifferenceInCumulativeCashFlow);
