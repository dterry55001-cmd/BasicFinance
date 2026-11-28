namespace BasicFinance.Web.Models;

public sealed record RetirementProjectionInput(
    int CurrentAge,
    int RetirementAge,
    decimal CurrentBalance,
    decimal MonthlyEmployeeContribution,
    decimal EmployerMatchPercent,
    decimal? EmployerMatchCapMonthly,
    decimal AnnualReturnPercent,
    decimal AnnualFeePercent,
    decimal AnnualInflationPercent,
    decimal AnnualContributionIncreasePercent,
    string CurrencyCode = "USD");

public sealed record RetirementProjectionYear(
    int Age,
    int MonthsElapsed,
    decimal EmployeeContributions,
    decimal EmployerContributions,
    decimal InvestmentGrowth,
    decimal ClosingBalance,
    decimal InflationAdjustedBalance);

public sealed record RetirementProjectionResult(
    string CurrencyCode,
    int YearsToRetirement,
    decimal StartingBalance,
    decimal TotalEmployeeContributions,
    decimal TotalEmployerContributions,
    decimal TotalInvestmentGrowth,
    decimal ProjectedBalance,
    decimal InflationAdjustedBalance,
    IReadOnlyList<RetirementProjectionYear> Years);
