using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class CreditMonitoringController(ApplicationDbContext database, AccountBalanceService balances) : Controller
{
    public async Task<IActionResult> Index(int? accountId, string? bureau, CancellationToken cancellationToken)
    {
        var eligible = database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && (a.Type == AccountType.CreditCard || a.DebtType == DebtAccountType.CreditCard || a.DebtType == DebtAccountType.LineOfCredit || a.DebtType == DebtAccountType.HomeEquityLineOfCredit));
        ViewBag.Accounts = await eligible.OrderBy(a => a.Name).ToListAsync(cancellationToken);
        ViewBag.SelectedAccountId = accountId;
        ViewBag.BureauFilter = bureau;
        var accounts = (IReadOnlyList<Account>)ViewBag.Accounts;
        var rows = new List<CreditUtilizationRow>();
        foreach (var account in accounts.Where(a => !accountId.HasValue || a.Id == accountId.Value))
        {
            var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            var limit = account.CreditLimit;
            decimal? utilization = limit is > 0 ? Math.Round(balance / limit.Value * 100m, 1) : null;
            var alert = utilization switch
            {
                >= 90m => "Critical",
                >= 75m => "High",
                >= 50m => "Elevated",
                >= 30m => "Watch",
                _ when utilization.HasValue => "Within threshold",
                _ => "Limit unavailable"
            };
            rows.Add(new CreditUtilizationRow(account, balance, limit, utilization, alert));
        }
        ViewBag.UtilizationRows = rows;
        var reported = database.ReportedBalanceSnapshots.AsNoTracking().Include(x => x.Account).AsQueryable();
        if (accountId.HasValue) reported = reported.Where(x => x.AccountId == accountId.Value);
        if (!string.IsNullOrWhiteSpace(bureau)) reported = reported.Where(x => x.Source == bureau);
        ViewBag.ReportedBalances = await reported.OrderByDescending(x => x.ReportedDate).ThenBy(x => x.Account.Name).Take(250).ToListAsync(cancellationToken);
        var limits = database.CreditLimitHistory.AsNoTracking().Include(x => x.Account).AsQueryable();
        if (accountId.HasValue) limits = limits.Where(x => x.AccountId == accountId.Value);
        ViewBag.LimitHistory = await limits.OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.Id).Take(250).ToListAsync(cancellationToken);
        var inquiries = database.CreditInquiries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(bureau)) inquiries = inquiries.Where(x => x.Bureau == bureau);
        var inquiryRows = await inquiries.OrderByDescending(x => x.InquiryDate).Take(250).ToListAsync(cancellationToken);
        ViewBag.Inquiries = inquiryRows;
        var currentYearInquiries = inquiryRows.Where(x => x.InquiryDate.Year == DateTime.Today.Year).ToList();
        ViewBag.HardInquiryCount = currentYearInquiries.Count(x => x.Type == CreditInquiryType.Hard);
        ViewBag.SoftInquiryCount = currentYearInquiries.Count(x => x.Type == CreditInquiryType.Soft);
        ViewBag.RecentHardInquiryCount = inquiryRows.Count(x => x.Type == CreditInquiryType.Hard && x.InquiryDate.Date >= DateTime.Today.AddDays(-365));
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReportedBalance(int accountId, DateTime reportedDate, decimal reportedBalance, decimal? reportedCreditLimit, string source, DateTime? receivedDate, string? notes, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.Status == AccountStatus.Active, cancellationToken);
        if (account is null) return NotFound();
        if (account.Type != AccountType.CreditCard && account.DebtType is not (DebtAccountType.CreditCard or DebtAccountType.LineOfCredit or DebtAccountType.HomeEquityLineOfCredit))
            ModelState.AddModelError(nameof(accountId), "Reported credit balances can only be recorded for revolving-credit accounts.");
        if (reportedDate == default) ModelState.AddModelError(nameof(reportedDate), "Enter the report date.");
        if (reportedBalance < 0) ModelState.AddModelError(nameof(reportedBalance), "Reported balance cannot be negative.");
        if (reportedCreditLimit < 0) ModelState.AddModelError(nameof(reportedCreditLimit), "Reported credit limit cannot be negative.");
        if (string.IsNullOrWhiteSpace(source) || source.Trim().Length > 40) ModelState.AddModelError(nameof(source), "Enter a source or bureau (up to 40 characters).");
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)); return RedirectToAction(nameof(Index), new { accountId }); }
        var cleanSource = source.Trim();
        var exists = await database.ReportedBalanceSnapshots.AnyAsync(x => x.AccountId == accountId && x.ReportedDate == reportedDate.Date && x.Source == cleanSource, cancellationToken);
        if (exists) { TempData["ErrorMessage"] = "A reported balance already exists for this account, date, and source."; return RedirectToAction(nameof(Index), new { accountId }); }
        database.ReportedBalanceSnapshots.Add(new ReportedBalanceSnapshot { AccountId = accountId, ReportedDate = reportedDate.Date, ReportedBalance = reportedBalance, ReportedCreditLimit = reportedCreditLimit, Source = cleanSource, ReceivedDate = receivedDate?.Date, Notes = Clean(notes) });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Reported balance saved. The account ledger balance was not changed.";
        return RedirectToAction(nameof(Index), new { accountId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInquiry(DateTime inquiryDate, string bureau, string lender, CreditInquiryType type, string? notes, CancellationToken cancellationToken)
    {
        if (inquiryDate == default) ModelState.AddModelError(nameof(inquiryDate), "Enter the inquiry date.");
        if (string.IsNullOrWhiteSpace(bureau) || bureau.Trim().Length > 60) ModelState.AddModelError(nameof(bureau), "Enter a bureau up to 60 characters.");
        if (string.IsNullOrWhiteSpace(lender) || lender.Trim().Length > 120) ModelState.AddModelError(nameof(lender), "Enter a lender up to 120 characters.");
        if (!Enum.IsDefined(type)) ModelState.AddModelError(nameof(type), "Choose Hard or Soft.");
        if (!ModelState.IsValid) { TempData["ErrorMessage"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)); return RedirectToAction(nameof(Index)); }
        database.CreditInquiries.Add(new CreditInquiry { InquiryDate = inquiryDate.Date, Bureau = bureau.Trim(), Lender = lender.Trim(), Type = type, Notes = Clean(notes) });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Credit inquiry saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInquiry(int id, CancellationToken cancellationToken)
    {
        var inquiry = await database.CreditInquiries.FindAsync(new object[] { id }, cancellationToken);
        if (inquiry is null) return NotFound();
        database.CreditInquiries.Remove(inquiry);
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Credit inquiry deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ImpactPlanner(CancellationToken cancellationToken)
    {
        await LoadImpactPlannerAccountsAsync(cancellationToken);
        return View(new CreditScoreImpactInput());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImpactPlanner(CreditScoreImpactInput input, CancellationToken cancellationToken)
    {
        var accounts = await LoadImpactPlannerAccountsAsync(cancellationToken);
        var selected = accounts.FirstOrDefault(a => a.Id == input.AccountId);
        if (selected is null) ModelState.AddModelError(nameof(input.AccountId), "Choose an active revolving-credit account.");
        if (selected is not null && selected.CreditLimit.GetValueOrDefault() + input.CreditLimitChange < 0)
            ModelState.AddModelError(nameof(input.CreditLimitChange), "The projected credit limit cannot be negative.");
        if (selected is not null)
        {
            var currentBalance = Math.Max(0m, await balances.GetBalanceAsync(selected.Id, cancellationToken));
            if (input.BalanceReduction > currentBalance)
                ModelState.AddModelError(nameof(input.BalanceReduction), "The hypothetical balance reduction cannot exceed the current balance.");
        }

        if (ModelState.IsValid)
        {
            var accountRows = new List<CreditScoreImpactAccount>();
            foreach (var account in accounts)
            {
                var balance = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
                var limit = Math.Max(0m, account.CreditLimit ?? 0m);
                var projectedBalance = balance;
                var projectedLimit = limit;
                if (account.Id == input.AccountId)
                {
                    projectedBalance = Math.Max(0m, balance - input.BalanceReduction);
                    projectedLimit = Math.Max(0m, limit + input.CreditLimitChange);
                }
                accountRows.Add(new CreditScoreImpactAccount(account.Id, account.Name, balance, limit,
                    limit > 0 ? balance / limit * 100m : 0m, projectedBalance, projectedLimit,
                    projectedLimit > 0 ? projectedBalance / projectedLimit * 100m : 0m));
            }

            var currentBalanceTotal = accountRows.Sum(x => x.Balance);
            var currentLimitTotal = accountRows.Sum(x => x.CreditLimit);
            var projectedBalanceTotal = accountRows.Sum(x => x.ProjectedBalance);
            var projectedLimitTotal = accountRows.Sum(x => x.ProjectedLimit);
            var today = DateTime.Today;
            var inquiries = await database.CreditInquiries.AsNoTracking()
                .Where(x => x.InquiryDate >= today.AddYears(-1) && x.InquiryDate <= today)
                .ToListAsync(cancellationToken);
            ViewBag.Result = new CreditScoreImpactResult
            {
                CurrentTotalBalance = currentBalanceTotal,
                CurrentTotalLimit = currentLimitTotal,
                CurrentUtilizationPercent = currentLimitTotal > 0 ? currentBalanceTotal / currentLimitTotal * 100m : 0m,
                ProjectedTotalBalance = projectedBalanceTotal,
                ProjectedTotalLimit = projectedLimitTotal,
                ProjectedUtilizationPercent = projectedLimitTotal > 0 ? projectedBalanceTotal / projectedLimitTotal * 100m : 0m,
                UtilizationChangePercentagePoints = (projectedLimitTotal > 0 ? projectedBalanceTotal / projectedLimitTotal * 100m : 0m) -
                    (currentLimitTotal > 0 ? currentBalanceTotal / currentLimitTotal * 100m : 0m),
                HardInquiriesLastTwelveMonths = inquiries.Count(x => x.Type == CreditInquiryType.Hard),
                SoftInquiriesLastTwelveMonths = inquiries.Count(x => x.Type == CreditInquiryType.Soft),
                Accounts = accountRows
            };
        }
        return View(input);
    }

    private async Task<List<Account>> LoadImpactPlannerAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active &&
                (a.Type == AccountType.CreditCard || a.DebtType == DebtAccountType.CreditCard ||
                 a.DebtType == DebtAccountType.LineOfCredit || a.DebtType == DebtAccountType.HomeEquityLineOfCredit))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        ViewBag.ImpactAccounts = accounts;
        return accounts;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreditUtilizationRow(Account Account, decimal LedgerBalance, decimal? CreditLimit, decimal? UtilizationPercent, string AlertLevel);
