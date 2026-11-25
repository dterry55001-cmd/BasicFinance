using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtConsolidationController(ApplicationDbContext database, AccountBalanceService balances) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await LoadAccountsAsync(cancellationToken);
        return View(new DebtConsolidationInput { FirstPaymentDate = DateTime.Today.AddMonths(1) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(DebtConsolidationInput input, CancellationToken cancellationToken)
    {
        var accounts = await LoadAccountsAsync(cancellationToken);
        var selectedIds = input.AccountIds.Distinct().ToArray();
        if (selectedIds.Length < 2)
            ModelState.AddModelError(nameof(input.AccountIds), "Select at least two debt accounts to compare consolidation.");
        var selected = accounts.Where(x => selectedIds.Contains(x.Id)).ToList();
        if (selected.Count != selectedIds.Length)
            ModelState.AddModelError(nameof(input.AccountIds), "One or more selected accounts are unavailable.");
        if (selected.Select(x => x.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            ModelState.AddModelError(nameof(input.AccountIds), "Only debts with the same currency can be consolidated in one comparison.");

        var debtRows = new List<DebtConsolidationDebt>();
        foreach (var account in selected)
        {
            var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            if (balance > 0)
                debtRows.Add(new DebtConsolidationDebt
                {
                    AccountId = account.Id, Name = account.Name, CurrencyCode = account.CurrencyCode,
                    Balance = balance, AnnualRatePercent = account.InterestRatePercent ?? 0m,
                    MinimumPayment = account.MinimumPayment ?? 0m
                });
        }
        if (debtRows.Count < 2)
            ModelState.AddModelError(nameof(input.AccountIds), "Select at least two accounts with outstanding balances.");

        if (ModelState.IsValid)
        {
            try { ViewBag.Result = Calculate(debtRows, input); }
            catch (ArgumentException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError(string.Empty, ex.Message); }
        }
        ViewBag.SelectedIds = selectedIds;
        return View(input);
    }

    private async Task<List<Account>> LoadAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var rows = new List<Account>();
        var accountBalances = new Dictionary<int, decimal>();
        foreach (var account in accounts)
        {
            var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            if (balance <= 0) continue;
            rows.Add(account);
            accountBalances[account.Id] = balance;
        }
        ViewBag.DebtAccounts = rows;
        ViewBag.AccountBalances = accountBalances;
        return rows;
    }

    private static DebtConsolidationResult Calculate(IReadOnlyList<DebtConsolidationDebt> debts, DebtConsolidationInput input)
    {
        if (input.AnnualRatePercent is < 0 or > 100 || input.OriginationFeePercent is < 0 or > 100 ||
            input.TermMonths is < 1 or > 600)
            throw new ArgumentOutOfRangeException(nameof(input), "Enter a valid APR, fee, and term.");
        var principal = debts.Sum(d => d.Balance);
        if (principal <= 0) throw new InvalidOperationException("Selected debts must have positive balances.");
        var fee = decimal.Round(principal * input.OriginationFeePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var financed = principal + fee;
        var monthlyRate = input.AnnualRatePercent / 1200m;
        var payment = monthlyRate == 0 ? financed / input.TermMonths :
            financed * monthlyRate / (1m - (decimal)Math.Pow((double)(1m + monthlyRate), -input.TermMonths));
        payment = decimal.Round(payment, 2, MidpointRounding.AwayFromZero);
        var balance = financed;
        var interest = 0m;
        var actualPaid = 0m;
        for (var month = 0; month < input.TermMonths && balance > 0; month++)
        {
            var monthlyInterest = decimal.Round(balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
            var due = balance + monthlyInterest;
            var installment = Math.Min(payment, due);
            if (installment <= monthlyInterest && balance > 0)
                throw new InvalidOperationException("The proposed payment does not reduce principal.");
            interest += monthlyInterest;
            actualPaid += installment;
            balance = Math.Max(0m, balance + monthlyInterest - installment);
        }
        if (balance > 0) throw new InvalidOperationException("The proposed term does not repay the consolidated balance.");
        return new DebtConsolidationResult
        {
            Principal = principal, OriginationFee = fee, FinancedAmount = financed,
            MonthlyPayment = payment, TotalInterest = interest, TotalCost = actualPaid,
            CurrentMonthlyMinimums = debts.Sum(d => d.MinimumPayment),
            CurrentWeightedApr = debts.Sum(d => d.Balance * d.AnnualRatePercent) / principal,
            ConsolidationApr = input.AnnualRatePercent, TermMonths = input.TermMonths,
            PayoffDate = input.FirstPaymentDate.Date.AddMonths(input.TermMonths - 1),
            Debts = debts
        };
    }
}
