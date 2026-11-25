using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtPaymentsController(ApplicationDbContext database, AccountBalanceService balances) : Controller
{
    private IQueryable<Account> DebtAccounts() => database.Accounts.Where(a => a.Status == AccountStatus.Active &&
        (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard));

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var start = (from ?? DateTime.Today.AddDays(-30)).Date;
        var end = (to ?? DateTime.Today.AddDays(90)).Date;
        if (end < start) (start, end) = (end, start);
        var records = await database.DebtPaymentRecords.AsNoTracking()
            .Include(p => p.Account).Include(p => p.SourceAccount)
            .Where(p => p.DueDate >= start && p.DueDate <= end)
            .OrderBy(p => p.DueDate).ThenBy(p => p.Account.Name).ToListAsync(cancellationToken);
        ViewBag.DebtAccounts = await DebtAccounts().AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);\n        ViewBag.From = start.ToString("yyyy-MM-dd");
        ViewBag.To = end.ToString("yyyy-MM-dd");
        ViewBag.OverdueCount = records.Count(p => p.IsPastDue(DateTime.Today));
        ViewBag.ScheduledTotalByCurrency = records.Where(p => p.Status == DebtPaymentStatus.Scheduled)
            .GroupBy(p => p.Account.CurrencyCode)
            .Select(g => new { CurrencyCode = g.Key, Amount = g.Sum(p => p.ExpectedAmount) }).ToList();
        return View(records);
    }

    // Backward-compatible entry point for links that use the GenerateSchedule name.
    [HttpGet]
    public IActionResult GenerateSchedule(int? accountId)
    {
        if (accountId.HasValue)
            return RedirectToAction(nameof(Schedule), new { accountId = accountId.Value });

        TempData["ErrorMessage"] = "Choose a debt or credit-card account before scheduling a payment.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Schedule(int accountId, CancellationToken cancellationToken)
    {
        var account = await DebtAccounts().AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null) return NotFound();
        ViewBag.Account = account;
        return View(new ScheduleDebtPaymentInput { AccountId = accountId,
            DueDate = NextDueDate(account.PaymentDueDay), ExpectedAmount = account.MinimumPayment ?? 0m });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(ScheduleDebtPaymentInput input, CancellationToken cancellationToken)
    {
        var account = await DebtAccounts().AsNoTracking().FirstOrDefaultAsync(a => a.Id == input.AccountId, cancellationToken);
        if (account is null) return NotFound();
        ViewBag.Account = account;
        input.DueDate = input.DueDate.Date;
        if (input.DueDate < DateTime.Today) ModelState.AddModelError(nameof(input.DueDate), "Choose today or a future due date.");
        if (await database.DebtPaymentRecords.AnyAsync(p => p.AccountId == input.AccountId && p.DueDate == input.DueDate, cancellationToken))
            ModelState.AddModelError(nameof(input.DueDate), "A payment is already scheduled for this account and date.");
        if (!ModelState.IsValid) return View(input);
        database.DebtPaymentRecords.Add(new DebtPaymentRecord {
            AccountId = input.AccountId, DueDate = input.DueDate, ExpectedAmount = input.ExpectedAmount,
            Notes = Clean(input.Notes), Status = DebtPaymentStatus.Scheduled
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Debt payment scheduled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Record(int id, CancellationToken cancellationToken)
    {
        var payment = await database.DebtPaymentRecords.AsNoTracking().Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payment is null || payment.Status != DebtPaymentStatus.Scheduled) return NotFound();
        await PopulateFundingAccounts(payment.Account.CurrencyCode, null, cancellationToken);
        ViewBag.Payment = payment;
        return View(new RecordDebtPaymentInput { PaymentRecordId = id, ActualAmount = payment.ExpectedAmount });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Record(RecordDebtPaymentInput input, CancellationToken cancellationToken)
    {
        var payment = await database.DebtPaymentRecords.Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == input.PaymentRecordId, cancellationToken);
        if (payment is null || payment.Status != DebtPaymentStatus.Scheduled) return NotFound();
        var source = await database.Accounts.FirstOrDefaultAsync(a => a.Id == input.SourceAccountId &&
            a.Status == AccountStatus.Active, cancellationToken);
        await PopulateFundingAccounts(payment.Account.CurrencyCode, input.SourceAccountId, cancellationToken);
        ViewBag.Payment = payment;

        var allocation = input.PrincipalPaid + input.InterestPaid + input.FeesPaid;
        if (source is null || source.Type is not (AccountType.Cash or AccountType.Checking or AccountType.Savings))
            ModelState.AddModelError(nameof(input.SourceAccountId), "Choose an active cash, checking, or savings funding account.");
        else if (!string.Equals(source.CurrencyCode, payment.Account.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(input.SourceAccountId), "Funding and debt accounts must use the same currency.");
        if (input.ActualAmount != allocation)
            ModelState.AddModelError(nameof(input.ActualAmount), "Actual payment must equal principal plus interest plus fees.");
        if (input.PaidDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(input.PaidDate), "A recorded payment date cannot be in the future.");
        if (input.PrincipalPaid > await balances.GetBalanceAsync(payment.AccountId, cancellationToken) && input.PrincipalPaid > 0)
            ModelState.AddModelError(nameof(input.PrincipalPaid), "Principal paid cannot exceed the current debt balance.");
        if (!ModelState.IsValid) return View(input);

        await using var dbTransaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var description = $"Debt payment - {payment.Account.Name}";
        FinancialTransaction? principalTransfer = null;
        if (input.PrincipalPaid > 0)
        {
            principalTransfer = new FinancialTransaction {
                AccountId = source!.Id, RelatedAccountId = payment.AccountId, Type = TransactionType.Transfer,
                Description = description + " (principal)", Amount = input.PrincipalPaid,
                CurrencyCode = source.CurrencyCode, TransactionDate = input.PaidDate.Date,
                CreatedUtc = now, UpdatedUtc = now
            };
            database.Transactions.Add(principalTransfer);
        }
        var nonPrincipal = input.InterestPaid + input.FeesPaid;
        if (nonPrincipal > 0)
        {
            database.Transactions.Add(new FinancialTransaction {
                AccountId = source!.Id, Type = TransactionType.Expense,
                Description = description + " (interest and fees)", Amount = nonPrincipal,
                CurrencyCode = source.CurrencyCode, TransactionDate = input.PaidDate.Date,
                CreatedUtc = now, UpdatedUtc = now
            });
        }
        await database.SaveChangesAsync(cancellationToken);
        payment.Status = DebtPaymentStatus.Paid;
        payment.PaidDate = input.PaidDate.Date;
        payment.ActualAmount = input.ActualAmount;
        payment.PrincipalPaid = input.PrincipalPaid;
        payment.InterestPaid = input.InterestPaid;
        payment.FeesPaid = input.FeesPaid;
        payment.SourceAccountId = source!.Id;
        payment.TransactionId = principalTransfer?.Id;
        payment.Notes = Clean(input.Notes) ?? payment.Notes;
        payment.UpdatedUtc = now;
        await database.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        TempData["SuccessMessage"] = "Payment recorded and linked transactions posted. Account balances have been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Skip(int id, CancellationToken cancellationToken)
    {
        var payment = await database.DebtPaymentRecords.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (payment is null || payment.Status != DebtPaymentStatus.Scheduled) return NotFound();
        payment.Status = DebtPaymentStatus.Skipped;
        payment.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Scheduled payment marked as skipped.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Progress(CancellationToken cancellationToken)
    {
        var accounts = await DebtAccounts().AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);
        var rows = new List<DebtProgressRow>();
        foreach (var account in accounts)
        {
            var current = Math.Max(0m, await balances.GetBalanceAsync(account.Id, cancellationToken));
            var earliestSnapshot = await database.AccountBalanceSnapshots.AsNoTracking()
                .Where(snapshot => snapshot.AccountId == account.Id)
                .OrderBy(snapshot => snapshot.SnapshotDate)
                .Select(snapshot => (decimal?)snapshot.Balance)
                .FirstOrDefaultAsync(cancellationToken);
            var configuredOriginal = account.OriginalLoanAmount.GetValueOrDefault();
            var original = configuredOriginal > 0m
                ? configuredOriginal
                : Math.Max(0m, earliestSnapshot.GetValueOrDefault() > 0m
                    ? earliestSnapshot.Value
                    : Math.Max(account.OpeningBalance, current));
            var reduced = Math.Clamp(original - current, 0m, original);
            var principal = await database.DebtPaymentRecords.AsNoTracking()
                .Where(p => p.AccountId == account.Id && p.Status == DebtPaymentStatus.Paid)
                .SumAsync(p => (decimal?)(p.PrincipalPaid ?? 0m), cancellationToken) ?? 0m;
            rows.Add(new DebtProgressRow {
                AccountId = account.Id, AccountName = account.Name, CurrencyCode = account.CurrencyCode,
                OriginalBalance = original, CurrentBalance = current, PrincipalReduced = reduced,
                PercentPaid = original > 0 ? Math.Clamp(reduced / original * 100m, 0m, 100m) : 0m,
                PrincipalRecorded = principal
            });
        }
        ViewBag.Rows = rows;
        ViewBag.Snapshots = await database.AccountBalanceSnapshots.AsNoTracking().Include(s => s.Account)
            .Where(s => DebtAccounts().Select(a => a.Id).Contains(s.AccountId))
            .OrderByDescending(s => s.SnapshotDate).Take(100).ToListAsync(cancellationToken);
        return View();
    }

    private async Task PopulateFundingAccounts(string currency, int? selected, CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status == AccountStatus.Active && a.CurrencyCode == currency &&
                (a.Type == AccountType.Checking || a.Type == AccountType.Savings || a.Type == AccountType.Cash))
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        ViewBag.FundingAccounts = new SelectList(accounts, "Id", "Name", selected);
    }

    private static DateTime NextDueDate(int? dueDay)
    {
        var today = DateTime.Today;
        var day = Math.Clamp(dueDay ?? today.Day, 1, DateTime.DaysInMonth(today.Year, today.Month));
        var candidate = new DateTime(today.Year, today.Month, day);
        if (candidate <= today)
        {
            var next = today.AddMonths(1);
            candidate = new DateTime(next.Year, next.Month, Math.Min(dueDay ?? day, DateTime.DaysInMonth(next.Year, next.Month)));
        }
        return candidate;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
