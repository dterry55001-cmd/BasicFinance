using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtReportingController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> TaxInterest(DebtTaxInterestReportFilter filter, CancellationToken cancellationToken)
    {
        filter.Year = filter.Year is >= 2000 and <= 2100 ? filter.Year : DateTime.Today.Year;
        filter.CurrencyCode = (filter.CurrencyCode ?? "USD").Trim().ToUpperInvariant();
        if (filter.CurrencyCode.Length != 3 || !filter.CurrencyCode.All(char.IsAsciiLetter))
            filter.CurrencyCode = "USD";

        var currencies = await database.Accounts.AsNoTracking()
            .Select(a => a.CurrencyCode).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        if (!currencies.Contains(filter.CurrencyCode)) currencies.Add(filter.CurrencyCode);
        ViewBag.Currencies = new SelectList(currencies, filter.CurrencyCode);

        var start = new DateTime(filter.Year, 1, 1);
        var end = start.AddYears(1);
        var interest = await database.InterestRecords.AsNoTracking().Include(x => x.Account)
            .Where(x => x.InterestDate >= start && x.InterestDate < end &&
                x.Account.CurrencyCode == filter.CurrencyCode)
            .ToListAsync(cancellationToken);
        var payments = await database.DebtPaymentRecords.AsNoTracking().Include(x => x.Account)
            .Where(x => x.Status == DebtPaymentStatus.Paid && x.PaidDate >= start && x.PaidDate < end &&
                x.Account.CurrencyCode == filter.CurrencyCode)
            .ToListAsync(cancellationToken);

        var report = new DebtTaxInterestReport
        {
            Year = filter.Year,
            CurrencyCode = filter.CurrencyCode,
            InterestCharged = interest.Where(x => x.Type == InterestRecordType.Charged).Sum(x => x.Amount),
            InterestEarned = interest.Where(x => x.Type == InterestRecordType.Earned).Sum(x => x.Amount),
            PrincipalPaid = payments.Sum(x => x.PrincipalPaid ?? 0m),
            DebtInterestPaid = payments.Sum(x => x.InterestPaid ?? 0m),
            FeesPaid = payments.Sum(x => x.FeesPaid ?? 0m),
            TotalDebtPayments = payments.Sum(x => x.ActualAmount ?? 0m),
            Accounts = interest.Select(x => x.AccountId).Concat(payments.Select(x => x.AccountId)).Distinct()
                .Select(id =>
                {
                    var accountInterest = interest.Where(x => x.AccountId == id).ToList();
                    var accountPayments = payments.Where(x => x.AccountId == id).ToList();
                    return new DebtTaxInterestAccountRow
                    {
                        AccountName = accountInterest.FirstOrDefault()?.Account.Name ??
                            accountPayments.First().Account.Name,
                        InterestCharged = accountInterest.Where(x => x.Type == InterestRecordType.Charged).Sum(x => x.Amount),
                        InterestEarned = accountInterest.Where(x => x.Type == InterestRecordType.Earned).Sum(x => x.Amount),
                        PrincipalPaid = accountPayments.Sum(x => x.PrincipalPaid ?? 0m),
                        DebtInterestPaid = accountPayments.Sum(x => x.InterestPaid ?? 0m),
                        FeesPaid = accountPayments.Sum(x => x.FeesPaid ?? 0m),
                        TotalPayments = accountPayments.Sum(x => x.ActualAmount ?? 0m)
                    };
                }).OrderBy(x => x.AccountName).ToList(),
            Monthly = Enumerable.Range(1, 12).Select(month =>
            {
                var monthInterest = interest.Where(x => x.InterestDate.Month == month).ToList();
                var monthPayments = payments.Where(x => x.PaidDate!.Value.Month == month).ToList();
                return new DebtTaxInterestMonthRow
                {
                    Month = month,
                    InterestCharged = monthInterest.Where(x => x.Type == InterestRecordType.Charged).Sum(x => x.Amount),
                    InterestEarned = monthInterest.Where(x => x.Type == InterestRecordType.Earned).Sum(x => x.Amount),
                    PrincipalPaid = monthPayments.Sum(x => x.PrincipalPaid ?? 0m),
                    DebtInterestPaid = monthPayments.Sum(x => x.InterestPaid ?? 0m),
                    FeesPaid = monthPayments.Sum(x => x.FeesPaid ?? 0m),
                    TotalPayments = monthPayments.Sum(x => x.ActualAmount ?? 0m)
                };
            }).ToList()
        };
        return View(report);
    }
}
