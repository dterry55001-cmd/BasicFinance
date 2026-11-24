using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class ImportsController(ApplicationDbContext database, StatementImportPreparationService preparation) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var imports = await database.StatementImports.AsNoTracking().Include(x => x.Account).Include(x => x.Rows)
            .OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync(cancellationToken);
        return View(imports);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateAccountsAsync(null, cancellationToken);
        PopulateProfiles(StatementSourceProfile.Unknown);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Create(int accountId, IFormFile? file, StatementSourceProfile profile, CancellationToken cancellationToken)
    {
        await PopulateAccountsAsync(accountId, cancellationToken);
        PopulateProfiles(profile);
        var account = await database.Accounts.FirstOrDefaultAsync(x => x.Id == accountId && x.Status != AccountStatus.Archived, cancellationToken);
        if (account is null) ModelState.AddModelError(nameof(accountId), "Choose an existing, non-archived account.");
        if (file is null || file.Length == 0) ModelState.AddModelError("file", "Choose a CSV statement file.");
        if (file is not null && file.Length > 10_000_000) ModelState.AddModelError("file", "The file must be 10 MB or smaller.");
        if (file is not null && !Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError("file", "Only CSV files are supported in this phase.");
        if (!ModelState.IsValid) return View();

        try
        {
            using var reader = new StreamReader(file!.OpenReadStream(), System.Text.Encoding.UTF8, true);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            var batch = await preparation.PrepareAsync(accountId, file.FileName, csv, profile, cancellationToken);
            return RedirectToAction(nameof(Preview), new { id = batch.Id });
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or InvalidOperationException)
        {
            ModelState.AddModelError("file", ex.Message);
            return View();
        }
    }

    public async Task<IActionResult> Preview(int id, CancellationToken cancellationToken)
    {
        var batch = await database.StatementImports.AsNoTracking().Include(x => x.Account)
            .Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (batch is null) return NotFound();
        return View(batch);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        var batch = await database.StatementImports.Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (batch is null) return NotFound();
        if (batch.Status != StatementImportStatus.Preview) return RedirectToAction(nameof(Preview), new { id });

        var account = await database.Accounts.FirstOrDefaultAsync(x => x.Id == batch.AccountId && x.Status != AccountStatus.Archived, cancellationToken);
        if (account is null) return BadRequest("The selected account is no longer available.");

        var seenDuringConfirmation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch.Rows.Where(x => x.ValidationError is null && !x.IsDuplicate && !x.IsImported && !x.RequiresReview))
        {
            var key = $"{row.TransactionDate:yyyy-MM-dd}|{row.Amount}|{row.Description.Trim()}";
            var exists = await database.Transactions.AnyAsync(t =>
                t.AccountId == batch.AccountId && t.TransactionDate == row.TransactionDate &&
                t.Amount == row.Amount && t.Description == row.Description, cancellationToken);
            if (exists || !seenDuringConfirmation.Add(key)) { row.IsDuplicate = true; continue; }

            database.Transactions.Add(new FinancialTransaction
            {
                AccountId = batch.AccountId,
                Type = row.Type,
                Description = row.Description,
                Category = row.Category,
                Amount = row.Amount,
                CurrencyCode = account.CurrencyCode,
                TransactionDate = row.TransactionDate!.Value,
                Reference = row.Reference,
                Activity = row.Activity,
                Symbol = row.Symbol,
                Cusip = row.Cusip,
                SecurityType = row.SecurityType,
                Quantity = row.Quantity,
                Price = row.Price,
                ExternalAccountNumberLast4 = row.ExternalAccountNumberLast4,
                FriendlyAccountName = row.FriendlyAccountName,
                Source = row.Source,
                FundName = row.FundName,
                UnitCount = row.UnitCount,
                UnitValue = row.UnitValue,
                PostingDate = row.PostingDate,
                EffectiveDate = row.EffectiveDate,
                ActivityDate = row.ActivityDate,
                ProcessDate = row.ProcessDate,
                SettlementDate = row.SettlementDate,
                SourceType = row.SourceType,
                SourceSubType = row.SourceSubType,
                ExtendedDescription = row.ExtendedDescription,
                SourceFieldsJson = row.SourceFieldsJson,
                SourceStatus = row.SourceStatus,
                SourceCategory = row.SourceCategory,
                SourceReference = row.SourceReference,
                Instrument = row.Instrument,
                Memo = row.Memo,
                CheckNumber = row.CheckNumber,
                ReferenceNumber = row.ReferenceNumber,
                TransactionCode = row.TransactionCode,
                SourceSignedAmount = row.SourceSignedAmount,
                RunningBalance = row.RunningBalance,
                ImportFingerprint = row.DuplicateFingerprint,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
            row.IsImported = true;
        }

        batch.Status = batch.Rows.Any(x => x.RequiresReview)
            ? StatementImportStatus.Preview
            : batch.Rows.Any(x => x.ValidationError is not null)
                ? StatementImportStatus.CompletedWithErrors
                : StatementImportStatus.Completed;
        batch.CompletedUtc = batch.Status == StatementImportStatus.Preview ? null : DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Import processing saved. {batch.Rows.Count(x => x.IsImported)} transaction(s) added; {batch.Rows.Count(x => x.IsDuplicate)} duplicate(s) skipped; {batch.Rows.Count(x => x.RequiresReview)} row(s) remain for review.";
        return RedirectToAction(nameof(Details), new { id });
    }


    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRobinhoodRow(
        int id, int rowId, TransactionType? normalizedType, CancellationToken cancellationToken)
    {
        var batch = await database.StatementImports.Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (batch is null) return NotFound();
        if (batch.Status != StatementImportStatus.Preview ||
            batch.Profile != StatementImportProfile.RobinhoodBrokerage)
            return BadRequest("Only Robinhood Brokerage rows in an open preview can be reviewed here.");

        var row = batch.Rows.SingleOrDefault(x => x.Id == rowId);
        if (row is null) return NotFound();
        if (!row.RequiresReview || row.ValidationError is not null || row.IsDuplicate || row.IsImported)
            return BadRequest("This row is not eligible for manual transaction-code review.");
        if (!normalizedType.HasValue || !Enum.IsDefined(normalizedType.Value) ||
            normalizedType.Value == TransactionType.Transfer)
            return BadRequest("Choose Income, Expense, Deposit, or Withdrawal. Transfers require a separate linked-account workflow.");

        row.Type = normalizedType.Value;
        row.RequiresReview = false;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Row {row.RowNumber} has been mapped to {normalizedType}. Confirm the import when the remaining rows are ready.";
        return RedirectToAction(nameof(Preview), new { id });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var batch = await database.StatementImports.AsNoTracking().Include(x => x.Account)
            .Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return batch is null ? NotFound() : View("Preview", batch);
    }

    private void PopulateProfiles(StatementSourceProfile selected)
    {
        ViewBag.Profiles = new SelectList(
            Enum.GetValues<StatementSourceProfile>()
                .Where(x => x != StatementSourceProfile.Unknown)
                .Select(x => new { Value = x, Text = x.ToString() }),
            "Value", "Text", selected);
    }

    private async Task PopulateAccountsAsync(int? selected, CancellationToken cancellationToken)
    {
        ViewBag.Accounts = new SelectList(await database.Accounts.AsNoTracking()
            .Where(x => x.Status != AccountStatus.Archived).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            "Id", "Name", selected);
    }
}
