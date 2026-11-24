using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class TransactionsController(
    ApplicationDbContext database,
    AccountBalanceService balances) : Controller
{
    public async Task<IActionResult> Index(
        int? accountId, DateTime? from, DateTime? to, string? search, CancellationToken cancellationToken)
    {
        var query = database.Transactions.AsNoTracking()
            .Include(transaction => transaction.Account)
            .Include(transaction => transaction.RelatedAccount)
            .AsQueryable();

        if (accountId.HasValue)
            query = query.Where(transaction => transaction.AccountId == accountId || transaction.RelatedAccountId == accountId);
        if (from.HasValue)
            query = query.Where(transaction => transaction.TransactionDate >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(transaction => transaction.TransactionDate < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(transaction => transaction.Description.Contains(term)
                || (transaction.Category != null && transaction.Category.Contains(term))
                || (transaction.Reference != null && transaction.Reference.Contains(term)));
        }

        ViewData["Accounts"] = new SelectList(
            await database.Accounts.AsNoTracking().Where(a => a.Status != AccountStatus.Archived).OrderBy(a => a.Name).ToListAsync(cancellationToken),
            "Id", "Name", accountId);
        ViewData["AccountId"] = accountId;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["Search"] = search;

        return View(await query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id).ToListAsync(cancellationToken));
    }

    public async Task<IActionResult> Create(int? accountId, CancellationToken cancellationToken)
    {
        await PopulateAccountsAsync(accountId, cancellationToken);
        return View(new TransactionInputModel { AccountId = accountId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionInputModel input, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.FindAsync([input.AccountId], cancellationToken);
        var destination = input.RelatedAccountId.HasValue
            ? await database.Accounts.FindAsync([input.RelatedAccountId.Value], cancellationToken)
            : null;

        ValidateAccounts(input, account, destination);
        if (!ModelState.IsValid)
        {
            await PopulateAccountsAsync(input.AccountId, cancellationToken);
            return View(input);
        }

        var transaction = new FinancialTransaction
        {
            AccountId = input.AccountId,
            RelatedAccountId = input.RelatedAccountId,
            Type = input.Type,
            Description = input.Description.Trim(),
            Category = Clean(input.Category),
            Amount = input.Amount,
            CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant(),
            TransactionDate = input.TransactionDate.Date,
            Reference = Clean(input.Reference),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        database.Transactions.Add(transaction);
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Transaction recorded.";
        return RedirectToAction(nameof(Index), new { accountId = input.AccountId });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var transaction = await database.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (transaction is null) return NotFound();

        await PopulateAccountsAsync(transaction.AccountId, cancellationToken);
        return View(new TransactionInputModel
        {
            AccountId = transaction.AccountId,
            RelatedAccountId = transaction.RelatedAccountId,
            Type = transaction.Type,
            Description = transaction.Description,
            Category = transaction.Category,
            Amount = transaction.Amount,
            CurrencyCode = transaction.CurrencyCode,
            TransactionDate = transaction.TransactionDate,
            Reference = transaction.Reference
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TransactionInputModel input, CancellationToken cancellationToken)
    {
        var transaction = await database.Transactions.FindAsync([id], cancellationToken);
        if (transaction is null) return NotFound();

        var account = await database.Accounts.FindAsync([input.AccountId], cancellationToken);
        var destination = input.RelatedAccountId.HasValue
            ? await database.Accounts.FindAsync([input.RelatedAccountId.Value], cancellationToken)
            : null;
        ValidateAccounts(input, account, destination);

        if (!ModelState.IsValid)
        {
            await PopulateAccountsAsync(input.AccountId, cancellationToken);
            return View(input);
        }

        transaction.AccountId = input.AccountId;
        transaction.RelatedAccountId = input.RelatedAccountId;
        transaction.Type = input.Type;
        transaction.Description = input.Description.Trim();
        transaction.Category = Clean(input.Category);
        transaction.Amount = input.Amount;
        transaction.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        transaction.TransactionDate = input.TransactionDate.Date;
        transaction.Reference = Clean(input.Reference);
        transaction.UpdatedUtc = DateTime.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Transaction updated.";
        return RedirectToAction(nameof(Index), new { accountId = input.AccountId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var transaction = await database.Transactions.FindAsync([id], cancellationToken);
        if (transaction is null) return NotFound();

        var accountId = transaction.AccountId;
        database.Transactions.Remove(transaction);
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Transaction deleted. Account balances have been recalculated.";
        return RedirectToAction(nameof(Index), new { accountId });
    }

    private void ValidateAccounts(TransactionInputModel input, Account? account, Account? destination)
    {
        if (account is null || account.Status == AccountStatus.Archived)
            ModelState.AddModelError(nameof(input.AccountId), "Choose an existing, non-archived account.");

        if (input.Type == TransactionType.Transfer)
        {
            if (destination is null || destination.Status == AccountStatus.Archived)
                ModelState.AddModelError(nameof(input.RelatedAccountId), "Choose an existing, non-archived destination account.");
            else if (account is not null && account.CurrencyCode != destination.CurrencyCode)
                ModelState.AddModelError(nameof(input.RelatedAccountId), "Transfers currently require matching account currencies.");
        }
    }

    private async Task PopulateAccountsAsync(int? selectedId, CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status != AccountStatus.Archived)
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        ViewBag.Accounts = new SelectList(accounts, "Id", "Name", selectedId);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
