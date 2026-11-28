using BasicFinance.Web.Services;

namespace BasicFinance.Web.Models;

public sealed record RetirementAccountOption(int AccountId, string Name, string CurrencyCode, decimal Balance,
    RetirementAccountType? RetirementType, decimal? EmployerMatchPercent, string? TaxTreatment);

public sealed class RetirementPlanningViewModel
{
    public IReadOnlyList<RetirementAccountOption> Accounts { get; init; } = [];
    public int? SelectedAccountId { get; set; }
    public int CurrentAge { get; set; } = 35;
    public int RetirementAge { get; set; } = 65;
    public decimal MonthlyEmployeeContribution { get; set; }
    public decimal? EmployerMatchCapMonthly { get; set; }
    public decimal AnnualReturnPercent { get; set; } = 6m;
    public decimal AnnualFeePercent { get; set; } = 0.25m;
    public decimal AnnualInflationPercent { get; set; } = 2.5m;
    public decimal AnnualContributionIncreasePercent { get; set; } = 2m;
    public RetirementProjectionResult? Projection { get; set; }
    public string? ErrorMessage { get; set; }
}
