using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace BasicFinance.Web.Controllers;

public sealed class RetirementPlanningController(
    IFinancialPlanningDataProvider dataProvider,
    RetirementProjectionCalculator calculator) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildModelAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Project(RetirementPlanningViewModel model, CancellationToken cancellationToken)
    {
        var asOfDate = DateOnly.FromDateTime(DateTime.Today);
        var data = await dataProvider.GetPlanningDataAsync(asOfDate, "USD", cancellationToken);
        var accounts = data.Accounts
            .Where(x => x.AccountType == AccountType.Retirement)
            .Select(x => new RetirementAccountOption(x.AccountId, x.Name, x.CurrencyCode, x.Balance,
                x.RetirementType, x.EmployerMatchPercent, x.TaxTreatment))
            .ToArray();
        model = CopyInputs(model, accounts);

        var selected = accounts.SingleOrDefault(x => x.AccountId == model.SelectedAccountId);
        if (selected is null)
        {
            model.ErrorMessage = "Select an active retirement account.";
            return View("Index", model);
        }
        if (selected.CurrencyCode != "USD")
        {
            model.ErrorMessage = "This projection currently supports USD accounts only. No currency conversion is assumed.";
            return View("Index", model);
        }

        try
        {
            model.Projection = calculator.Calculate(new RetirementProjectionInput(
                model.CurrentAge, model.RetirementAge, selected.Balance,
                model.MonthlyEmployeeContribution, selected.EmployerMatchPercent ?? 0m,
                model.EmployerMatchCapMonthly, model.AnnualReturnPercent,
                model.AnnualFeePercent, model.AnnualInflationPercent,
                model.AnnualContributionIncreasePercent, selected.CurrencyCode));
            if (selected.RetirementType is RetirementAccountType.DefinedBenefitPension
                or RetirementAccountType.CashBalancePension or RetirementAccountType.Annuity)
                model.ErrorMessage = "This account type may represent a pension or annuity income stream. The balance projection is illustrative only; a separate income projection is needed.";
        }
        catch (ArgumentException exception)
        {
            model.ErrorMessage = exception.Message;
        }

        return View("Index", model);
    }

    private async Task<RetirementPlanningViewModel> BuildModelAsync(DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var data = await dataProvider.GetPlanningDataAsync(asOfDate, "USD", cancellationToken);
        return new RetirementPlanningViewModel
        {
            Accounts = data.Accounts.Where(x => x.AccountType == AccountType.Retirement)
                .Select(x => new RetirementAccountOption(x.AccountId, x.Name, x.CurrencyCode, x.Balance,
                    x.RetirementType, x.EmployerMatchPercent, x.TaxTreatment)).ToArray()
        };
    }

    private static RetirementPlanningViewModel CopyInputs(
        RetirementPlanningViewModel source, IReadOnlyList<RetirementAccountOption> accounts) => new()
    {
        Accounts = accounts,
        SelectedAccountId = source.SelectedAccountId,
        CurrentAge = source.CurrentAge,
        RetirementAge = source.RetirementAge,
        MonthlyEmployeeContribution = source.MonthlyEmployeeContribution,
        EmployerMatchCapMonthly = source.EmployerMatchCapMonthly,
        AnnualReturnPercent = source.AnnualReturnPercent,
        AnnualFeePercent = source.AnnualFeePercent,
        AnnualInflationPercent = source.AnnualInflationPercent,
        AnnualContributionIncreasePercent = source.AnnualContributionIncreasePercent
    };
}
