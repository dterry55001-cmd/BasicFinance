using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtAnalyticsController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? year, CancellationToken cancellationToken)
    {
        var paidPayments = await database.DebtPaymentRecords.AsNoTracking()
            .Where(p => p.Status == DebtPaymentStatus.Paid && p.PaidDate != null)
            .Include(p => p.Account)
            .OrderBy(p => p.PaidDate)
            .ToListAsync(cancellationToken);

        var availableYears = paidPayments.Select(p => p.PaidDate!.Value.Year).ToList();
        var currentYear = DateTime.Today.Year;
        var selectedYear = year ?? (availableYears.Count > 0 ? availableYears.Max() : currentYear);
        if (selectedYear < 1900 || selectedYear > currentYear + 1) selectedYear = currentYear;

        var selected = paidPayments.Where(p => p.PaidDate!.Value.Year == selectedYear).ToList();
        var model = new DebtAnalyticsViewModel
        {
            SelectedYear = selectedYear,
            AvailableYearsStart = availableYears.Count > 0 ? Math.Min(availableYears.Min(), currentYear) : currentYear,
            AvailableYearsEnd = Math.Max(currentYear, availableYears.Count > 0 ? availableYears.Max() : currentYear)
        };

        foreach (var group in paidPayments.GroupBy(p => p.Account.CurrencyCode).OrderBy(g => g.Key))
        {
            var principal = group.Sum(p => p.PrincipalPaid ?? 0m);
            var interest = group.Sum(p => p.InterestPaid ?? 0m);
            var fees = group.Sum(p => p.FeesPaid ?? 0m);
            var total = group.Sum(p => p.ActualAmount ?? p.ExpectedAmount);
            model.CurrencySummaries.Add(new DebtAnalyticsCurrencySummary(
                group.Key, principal, interest, fees, total,
                principal > 0m ? decimal.Round(interest / principal * 100m, 1) : 0m, group.Count()));
        }

        model.MonthlyBreakdown = selected
            .GroupBy(p => new { p.Account.CurrencyCode, Month = new DateTime(p.PaidDate!.Value.Year, p.PaidDate.Value.Month, 1) })
            .Select(g => new DebtMonthlyAnalyticsRow(
                g.Key.CurrencyCode, g.Key.Month,
                g.Sum(p => p.PrincipalPaid ?? 0m),
                g.Sum(p => p.InterestPaid ?? 0m),
                g.Sum(p => p.FeesPaid ?? 0m),
                g.Sum(p => p.ActualAmount ?? p.ExpectedAmount),
                g.Count()))
            .OrderBy(x => x.CurrencyCode).ThenBy(x => x.Month)
            .ToList();

        model.AnnualReports = paidPayments
            .GroupBy(p => new { p.Account.CurrencyCode, Year = p.PaidDate!.Value.Year })
            .Select(g => new DebtAnnualReportRow(
                g.Key.CurrencyCode, g.Key.Year,
                g.Sum(p => p.PrincipalPaid ?? 0m),
                g.Sum(p => p.InterestPaid ?? 0m),
                g.Sum(p => p.FeesPaid ?? 0m),
                g.Sum(p => p.ActualAmount ?? p.ExpectedAmount),
                g.Count()))
            .OrderByDescending(x => x.Year).ThenBy(x => x.CurrencyCode)
            .ToList();

        var revolvingAccounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active &&
                (a.Type == AccountType.CreditCard ||
                 a.DebtType == DebtAccountType.CreditCard ||
                 a.DebtType == DebtAccountType.LineOfCredit ||
                 a.DebtType == DebtAccountType.HomeEquityLineOfCredit))
            .Select(a => new { a.Id, a.CurrencyCode })
            .ToListAsync(cancellationToken);
        var accountCurrencies = revolvingAccounts.ToDictionary(a => a.Id, a => a.CurrencyCode);
        var revolvingIds = accountCurrencies.Keys.ToList();

        if (revolvingIds.Count > 0)
        {
            var reports = await database.ReportedBalanceSnapshots.AsNoTracking()
                .Where(s => revolvingIds.Contains(s.AccountId))
                .OrderBy(s => s.ReportedDate)
                .ToListAsync(cancellationToken);

            model.UtilizationTrends = reports
                .GroupBy(s => new { Currency = accountCurrencies[s.AccountId], Month = new DateTime(s.ReportedDate.Year, s.ReportedDate.Month, 1) })
                .Select(g =>
                {
                    var latestByAccount = g.GroupBy(s => s.AccountId)
                        .Select(accountGroup => accountGroup.OrderByDescending(s => s.ReportedDate).ThenByDescending(s => s.Id).First())
                        .ToList();
                    var balance = latestByAccount.Sum(s => Math.Max(0m, s.ReportedBalance));
                    var limit = latestByAccount.Sum(s => Math.Max(0m, s.ReportedCreditLimit ?? 0m));
                    return limit > 0m
                        ? new CreditUtilizationTrendRow(g.Key.Currency, g.Key.Month, balance, limit,
                            decimal.Round(balance / limit * 100m, 1), latestByAccount.Count)
                        : null;
                })
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderBy(x => x.CurrencyCode).ThenBy(x => x.Month)
                .ToList();
        }

        return View(model);
    }
}
