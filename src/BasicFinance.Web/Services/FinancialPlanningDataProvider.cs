using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Services;

public enum PlanningSourceStatus { Available, Partial, Missing, Unavailable }

public sealed record PlanningSourceMetadata(string SourceName, PlanningSourceStatus Status, DateTime RetrievedUtc, string? Warning = null);
public sealed record PlanningAccountInput(int AccountId, string Name, AccountType AccountType, string CurrencyCode, decimal Balance, decimal? InterestRatePercent, decimal? CreditLimit, decimal? MinimumPayment, int? PaymentDueDay, RetirementAccountType? RetirementType, decimal? EmployerMatchPercent, string? TaxTreatment);
public sealed record PlanningDebtInput(int AccountId, string Name, DebtAccountType? DebtType, string CurrencyCode, decimal Balance, decimal? AnnualRatePercent, decimal? MinimumPayment, int? PaymentDueDay);
public sealed record PlanningBudgetInput(int BudgetPeriodId, int Year, int Month, string CurrencyCode, int CategoryId, string CategoryName, decimal PlannedAmount, decimal RolloverAmount);
public sealed record PlanningSavingsGoalInput(int GoalId, string Name, string CurrencyCode, decimal TargetAmount, decimal CurrentAmount, DateTime? TargetDate, bool IsEmergencyFund, int? LinkedAccountId);
public sealed record PlanningSavingsScheduleInput(int GoalId, string GoalName, string CurrencyCode, decimal Amount, SavingsScheduleFrequency Frequency, DateTime NextDueDate, DateTime? EndDate);
public sealed record PlanningCashFlowScheduleInput(int ScheduleId, int AccountId, string Description, TransactionType Type, string CurrencyCode, decimal Amount, CashFlowFrequency Frequency, DateTime NextDueDate, DateTime? EndDate);
public sealed record FinancialPlanningData(DateOnly AsOfDate, string RequestedReportingCurrency, IReadOnlyList<PlanningAccountInput> Accounts, IReadOnlyList<PlanningDebtInput> Debts, IReadOnlyList<PlanningBudgetInput> Budgets, IReadOnlyList<PlanningSavingsGoalInput> SavingsGoals, IReadOnlyList<PlanningSavingsScheduleInput> FutureSavingsSchedules, IReadOnlyList<PlanningCashFlowScheduleInput> FutureCashFlowSchedules, IReadOnlyList<PlanningSourceMetadata> Sources, IReadOnlyList<string> Warnings);

public interface IFinancialPlanningDataProvider
{
    Task<FinancialPlanningData> GetPlanningDataAsync(DateOnly asOfDate, string reportingCurrency, CancellationToken cancellationToken = default);
}

/// <summary>Read-only composition boundary for Financial Planning; never writes to source modules.</summary>
public sealed class FinancialPlanningDataProvider(ApplicationDbContext database, AccountBalanceService accountBalances) : IFinancialPlanningDataProvider
{
    public async Task<FinancialPlanningData> GetPlanningDataAsync(DateOnly asOfDate, string reportingCurrency, CancellationToken cancellationToken = default)
    {
        ValidateCurrency(reportingCurrency);
        var cutoff = asOfDate.ToDateTime(TimeOnly.MaxValue);
        var retrievedUtc = DateTime.UtcNow;
        var warnings = new List<string>();
        var sources = new List<PlanningSourceMetadata>();

        var accounts = await database.Accounts.AsNoTracking().Where(x => x.Status == AccountStatus.Active).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var accountInputs = new List<PlanningAccountInput>(accounts.Count);
        foreach (var account in accounts)
        {
            var balance = await accountBalances.GetBalanceAtAsync(account.Id, cutoff, cancellationToken);
            accountInputs.Add(new PlanningAccountInput(account.Id, account.Name, account.Type, account.CurrencyCode, balance,
                account.InterestRatePercent, account.CreditLimit, account.MinimumPayment, account.PaymentDueDay,
                account.RetirementType, account.EmployerMatchPercent, account.TaxTreatment));
        }
        sources.Add(new("Accounts", accounts.Count == 0 ? PlanningSourceStatus.Missing : PlanningSourceStatus.Available, retrievedUtc,
            accounts.Count == 0 ? "No active accounts were found." : null));

        var debts = accountInputs.Where(x => x.AccountType is AccountType.Debt or AccountType.CreditCard)
            .Select(x => new PlanningDebtInput(x.AccountId, x.Name, accounts.Single(a => a.Id == x.AccountId).DebtType,
                x.CurrencyCode, x.Balance, x.InterestRatePercent, x.MinimumPayment, x.PaymentDueDay)).ToArray();
        var debtWarnings = debts.Where(x => x.AnnualRatePercent is null || x.MinimumPayment is null)
            .Select(x => $"Debt account '{x.Name}' is missing " +
                (x.AnnualRatePercent is null && x.MinimumPayment is null ? "an interest rate and minimum payment." :
                 x.AnnualRatePercent is null ? "an interest rate." : "a minimum payment.")).ToArray();
        warnings.AddRange(debtWarnings);
        sources.Add(new("Debt Management", debts.Length == 0 ? PlanningSourceStatus.Missing :
            debtWarnings.Length > 0 ? PlanningSourceStatus.Partial : PlanningSourceStatus.Available, retrievedUtc,
            debtWarnings.Length == 0 ? null : $"{debtWarnings.Length} debt account(s) need rate or payment details."));

        var budgetStart = new DateTime(asOfDate.Year, asOfDate.Month, 1);
        var budgetPeriods = await database.BudgetPeriods.AsNoTracking()
            .Include(x => x.Lines)
                .ThenInclude(line => line.Category)
            .Where(x => x.Year == asOfDate.Year && x.Month == asOfDate.Month)
            .ToListAsync(cancellationToken);
        var budgets = budgetPeriods
            .SelectMany(period => period.Lines.Select(line => new PlanningBudgetInput(
                period.Id, period.Year, period.Month, period.CurrencyCode,
                line.CategoryId, line.Category.Name, line.PlannedAmount, line.RolloverAmount)))
            .ToArray();
        sources.Add(new("Budget Management", budgets.Count == 0 ? PlanningSourceStatus.Missing :
            budgets.Any(x => x.CurrencyCode != reportingCurrency) ? PlanningSourceStatus.Partial : PlanningSourceStatus.Available,
            retrievedUtc, budgets.Count == 0 ? $"No budget lines were found for {budgetStart:yyyy-MM}." :
            budgets.Any(x => x.CurrencyCode != reportingCurrency) ? "Budget data includes other currencies; amounts are not converted." : null));

        var goals = await database.SavingsGoals.AsNoTracking().Where(x => x.Status != SavingsGoalStatus.Archived)
            .Select(x => new
            {
                x.Id, x.Name, x.CurrencyCode, x.TargetAmount, x.TargetDate, x.IsEmergencyFund, x.LinkedAccountId,
                Contributions = x.Contributions.Where(c => c.ContributionDate <= cutoff)
                    .Select(c => new { c.Amount, c.Type })
            }).ToListAsync(cancellationToken);
        var savings = goals.Select(x => new PlanningSavingsGoalInput(x.Id, x.Name, x.CurrencyCode, x.TargetAmount,
            x.Contributions.Sum(c => c.Type == SavingsContributionType.Contribution ? c.Amount : -c.Amount),
            x.TargetDate, x.IsEmergencyFund, x.LinkedAccountId)).ToArray();
        var futureSavings = await database.RecurringSavingsContributions.AsNoTracking()
            .Where(x => x.IsActive && x.NextDueDate > cutoff && (x.EndDate == null || x.NextDueDate <= x.EndDate))
            .Select(x => new PlanningSavingsScheduleInput(x.SavingsGoalId, x.SavingsGoal.Name, x.SavingsGoal.CurrencyCode,
                x.Amount, x.Frequency, x.NextDueDate, x.EndDate)).ToListAsync(cancellationToken);
        sources.Add(new("Savings Management", savings.Length == 0 ? PlanningSourceStatus.Missing : PlanningSourceStatus.Available,
            retrievedUtc, savings.Length == 0 ? "No savings goals were found." : null));

        var futureCashSchedules = await database.RecurringCashFlows.AsNoTracking()
            .Where(x => x.IsActive && x.NextDueDate > cutoff && (x.EndDate == null || x.NextDueDate <= x.EndDate))
            .Select(x => new PlanningCashFlowScheduleInput(x.Id, x.AccountId, x.Description, x.Type, x.CurrencyCode,
                x.Amount, x.Frequency, x.NextDueDate, x.EndDate)).ToListAsync(cancellationToken);
        sources.Add(new("Cash Flow Forecasting", futureCashSchedules.Count == 0 ? PlanningSourceStatus.Missing : PlanningSourceStatus.Available,
            retrievedUtc, futureCashSchedules.Count == 0 ? "No future active recurring cash-flow schedules were found." : null));

        if (accounts.Any(x => x.CurrencyCode != reportingCurrency) || budgets.Any(x => x.CurrencyCode != reportingCurrency) ||
            savings.Any(x => x.CurrencyCode != reportingCurrency) || futureSavings.Any(x => x.CurrencyCode != reportingCurrency) ||
            futureCashSchedules.Any(x => x.CurrencyCode != reportingCurrency))
            warnings.Add($"Some planning records are not denominated in {reportingCurrency}. No FX conversion was applied; keep amounts grouped by currency.");
        if (savings.Any(x => x.LinkedAccountId.HasValue))
            warnings.Add("Savings goal balances are progress metadata, not additional assets. Use linked account balances as the asset source.");
        if (accountInputs.Any(x => x.AccountType == AccountType.Retirement))
            warnings.Add("Retirement balances remain in account balances; disclose withdrawal restrictions, taxes, and penalties separately. Pension and annuity income need separate treatment.");

        return new FinancialPlanningData(asOfDate, reportingCurrency, accountInputs, debts, budgets, savings,
            futureSavings, futureCashSchedules, sources, warnings);
    }

    private static void ValidateCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || currency.Any(c => !char.IsAsciiLetterUpper(c)))
            throw new ArgumentException("Reporting currency must be a three-letter uppercase code.", nameof(currency));
    }
}