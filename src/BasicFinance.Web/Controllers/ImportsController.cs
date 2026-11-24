using System.Text;
using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class ImportsController(ApplicationDbContext database, CsvStatementParser parser) : Controller
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
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Create(int accountId, IFormFile? file, CancellationToken cancellationToken)
    {
        await PopulateAccountsAsync(accountId, cancellationToken);
        var account = await database.Accounts.FirstOrDefaultAsync(x => x.Id == accountId && x.Status != AccountStatus.Archived, cancellationToken);
        if (account is null) ModelState.AddModelError(nameof(accountId), "Choose an existing, non-archived account.");
        if (file is null || file.Length == 0) ModelState.AddModelError("file", "Choose a CSV statement file.");
        if (file is not null && file.Length > 10_000_000) ModelState.AddModelError("file", "The file must be 10 MB or smaller.");
        if (file is not null && !Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError("file", "Only CSV files are supported in this phase.");
        if (!ModelState.IsValid) return View();

        try
        {
            using var reader = new StreamReader(file!.OpenReadStream(), Encoding.UTF8, true);
            var csv = await reader.ReadToEndAsync(cancellationToken);
            var rows = parser.Parse(csv);
            var batch = new StatementImport
            {
                FileName = Path.GetFileName(file.FileName),
                AccountId = accountId,
                Status = StatementImportStatus.Preview,
                CreatedUtc = DateTime.UtcNow,
                Rows = rows
            };
            var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows.Where(x => x.ValidationError is null))
            {
                var key = $"{row.TransactionDate:yyyy-MM-dd}|{row.Amount}|{row.Description.Trim()}";
                var exists = await database.Transactions.AnyAsync(t =>
                    t.AccountId == accountId && t.TransactionDate == row.TransactionDate &&
                    t.Amount == row.Amount && t.Description == row.Description, cancellationToken);
                row.IsDuplicate = exists || !seenInFile.Add(key);
            }
            database.StatementImports.Add(batch);
            await database.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Preview), new { id = batch.Id });
        }
        catch (InvalidDataException ex)
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
        foreach (var row in batch.Rows.Where(x => x.ValidationError is null && !x.IsDuplicate))
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
                Source = row.Source,
                FundName = row.FundName,
                UnitCount = row.UnitCount,
                UnitValue = row.UnitValue,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
            row.IsImported = true;
        }

        batch.Status = batch.Rows.Any(x => x.ValidationError is not null)
            ? StatementImportStatus.CompletedWithErrors : StatementImportStatus.Completed;
        batch.CompletedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Import completed. {batch.Rows.Count(x => x.IsImported)} transaction(s) added; {batch.Rows.Count(x => x.IsDuplicate)} duplicate(s) skipped.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var batch = await database.StatementImports.AsNoTracking().Include(x => x.Account)
            .Include(x => x.Rows).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return batch is null ? NotFound() : View("Preview", batch);
    }

    private async Task PopulateAccountsAsync(int? selected, CancellationToken cancellationToken)
    {
        ViewBag.Accounts = new SelectList(await database.Accounts.AsNoTracking()
            .Where(x => x.Status != AccountStatus.Archived).OrderBy(x => x.Name).ToListAsync(cancellationToken),
            "Id", "Name", selected);
    }
}
