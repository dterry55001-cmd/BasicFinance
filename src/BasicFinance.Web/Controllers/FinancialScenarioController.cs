using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class FinancialScenarioController(
    ApplicationDbContext database,
    IFinancialPlanningDataProvider dataProvider,
    FinancialScenarioCalculator calculator) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await BuildBaselineAsync(cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Compare(FinancialScenarioViewModel input, CancellationToken cancellationToken)
    {
        var model = await BuildBaselineAsync(cancellationToken);
        model.MonthlyIncomeChange = input.MonthlyIncomeChange;
        model.MonthlyExpenseChange = input.MonthlyExpenseChange;
        model.AnnualDebtRateChange = input.AnnualDebtRateChange;
        model.MonthlyDebtPaymentChange = input.MonthlyDebtPaymentChange;
        model.MonthlySavingsContributionChange = input.MonthlySavingsContributionChange;
        model.ProjectionMonths = input.ProjectionMonths;

        if (model.MonthlyIncome + input.MonthlyIncomeChange < 0m)
            ModelState.AddModelError(nameof(input.MonthlyIncomeChange), "The resulting monthly income cannot be negative.");
        if (model.MonthlyExpenses + input.MonthlyExpenseChange < 0m)
            ModelState.AddModelError(nameof(input.MonthlyExpenseChange), "The resulting monthly expenses cannot be negative.");

        if (ModelState.IsValid)
        {
            try
            {
                var scenarioAssumptions = new FinancialScenarioAssumptions(
                    input.MonthlyIncomeChange, input.MonthlyExpenseChange,
                    input.AnnualDebtRateChange, input.MonthlyDebtPaymentChange,
                    input.MonthlySavingsContributionChange);
                var baselineInput = new FinancialScenarioInput(
                    model.StartingDebtBalance, model.AnnualDebtRatePercent, model.MonthlyDebtPayment,
                    model.StartingSavingsBalance, model.MonthlySavingsContribution,
                    model.MonthlyIncome, model.MonthlyExpenses, model.ProjectionMonths,
                    new FinancialScenarioAssumptions());
                model.Comparison = calculator.Compare(baselineInput, scenarioAssumptions, scenarioName: "Adjusted plan");
            }
            catch (ArgumentException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
            }
        }
        if (!ModelState.IsValid)
            model.Warnings = model.Warnings.Append("Correct the scenario assumptions and submit again.").ToArray();
        return View("Index", model);
    }

    private async Task<FinancialScenarioViewModel> BuildBaselineAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var data = await dataProvider.GetPlanningDataAsync(today, "USD", cancellationToken);
        var debts = data.Debts.Where(x => x.CurrencyCode == "USD" && x.Balance > 0m).ToArray();
        var debtBalance = debts.Sum(x => x.Balance);
        var debtPayment = debts.Sum(x => x.MinimumPayment ?? 0m);
        var weightedRate = debtBalance == 0m ? 0m : debts.Sum(x => x.Balance * (x.AnnualRatePercent ?? 0m)) / debtBalance;

        var goals = data.SavingsGoals.Where(x => x.CurrencyCode == "USD").ToArray();
        var savingsProgress = goals.Sum(x => Math.Max(0m, x.CurrentAmount));
        var schedules = data.FutureSavingsSchedules.Where(x => x.CurrencyCode == "USD");
        var monthlySavings = schedules.Sum(x => x.Frequency switch
        {
            SavingsScheduleFrequency.Weekly => x.Amount * 52m / 12m,
            SavingsScheduleFrequency.Biweekly => x.Amount * 26m / 12m,
            SavingsScheduleFrequency.Monthly => x.Amount,
            SavingsScheduleFrequency.Quarterly => x.Amount / 3m,
            SavingsScheduleFrequency.Yearly => x.Amount / 12m,
            _ => 0m
        });

        var monthStart = new DateTime(today.Year, today.Month, 1);
        var incomeStart = monthStart.AddMonths(-3);
        var incomeAmounts = await database.Transactions.AsNoTracking()
            .Where(x => x.CurrencyCode == "USD" && x.Type == TransactionType.Income
                && x.TransactionDate >= incomeStart && x.TransactionDate < monthStart)
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken);
        var budgets = data.Budgets.Where(x => x.CurrencyCode == "USD").ToArray();
        var monthlyExpenses = budgets.Sum(x => x.PlannedAmount + x.RolloverAmount);

        var warnings = data.Warnings.ToList();
        warnings.Add("Income baseline is the average of the previous three completed months of recorded USD income.");
        warnings.Add(budgets.Length == 0
            ? "No current-month USD budget lines were found; the expense baseline is zero until a budget is entered."
            : "Expense baseline uses the current month's planned USD budget, including rollover.");
        warnings.Add("Savings baseline is the sum of savings-goal progress for planning comparison only. It is not an additional asset balance.");
        warnings.Add("Only USD records are included. Other currencies are excluded and are not converted.");
        return new FinancialScenarioViewModel
        {
            StartingDebtBalance = debtBalance,
            AnnualDebtRatePercent = weightedRate,
            MonthlyDebtPayment = debtPayment,
            StartingSavingsBalance = savingsProgress,
            MonthlySavingsContribution = monthlySavings,
            MonthlyIncome = incomeAmounts.Sum() / 3m,
            MonthlyExpenses = monthlyExpenses,
            Warnings = warnings.Distinct().ToArray()
        };
    }
}
