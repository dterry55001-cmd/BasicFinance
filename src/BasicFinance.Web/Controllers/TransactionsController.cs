using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class TransactionsController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index(
        int? accountId, DateTime? from, DateTime? to, string? search, int? categoryId, CancellationToken cancellationToken)
    {
        var query = database.Transactions.AsNoTracking()
            .Include(transaction => transaction.Account)
            .Include(transaction => transaction.RelatedAccount)
            .Include(transaction => transaction.TransactionCategory)
            .AsQueryable();

        if (accountId.HasValue)
            query = query.Where(transaction => transaction.AccountId == accountId || transaction.RelatedAccountId == accountId);
        if (from.HasValue)
            query = query.Where(transaction => transaction.TransactionDate >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(transaction => transaction.TransactionDate < to.Value.Date.AddDays(1));
        if (categoryId.HasValue)
            query = query.Where(transaction => transaction.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(transaction => transaction.Description.Contains(term)
                || (transaction.Category != null && transaction.Category.Contains(term))
                || (transaction.TransactionCategory != null && transaction.TransactionCategory.Name.Contains(term))
                || (transaction.Reference != null && transaction.Reference.Contains(term))
                || (transaction.Symbol != null && transaction.Symbol.Contains(term))
                || (transaction.Cusip != null && transaction.Cusip.Contains(term))
                || (transaction.Activity != null && transaction.Activity.Contains(term)));
        }

        ViewData["Accounts"] = new SelectList(
            await database.Accounts.AsNoTracking().Where(a => a.Status != AccountStatus.Archived).OrderBy(a => a.Name).ToListAsync(cancellationToken),
            "Id", "Name", accountId);
        ViewData["Categories"] = new SelectList(
            await database.TransactionCategories.AsNoTracking().OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync(cancellationToken),
            "Id", "Name", categoryId);
        ViewData["AccountId"] = accountId;
        ViewData["CategoryId"] = categoryId;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["Search"] = search;

        return View(await query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id).ToListAsync(cancellationToken));
    }

    public async Task<IActionResult> Create(int? accountId, CancellationToken cancellationToken)
    {
        await PopulateFormAsync(accountId, null, null, cancellationToken);
        return View(new TransactionInputModel { AccountId = accountId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionInputModel input, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.FindAsync([input.AccountId], cancellationToken);
        var destination = input.RelatedAccountId.HasValue
            ? await database.Accounts.FindAsync([input.RelatedAccountId.Value], cancellationToken)
            : null;

        await ValidateCategoryAsync(input, cancellationToken);
        ValidateAccounts(input, account, destination);
        if (!ModelState.IsValid)
        {
            await PopulateFormAsync(input.AccountId, input.Type, input.CategoryId, cancellationToken);
            return View(input);
        }

        var transaction = new FinancialTransaction
        {
            AccountId = input.AccountId,
            RelatedAccountId = input.RelatedAccountId,
            Type = input.Type,
            Description = input.Description.Trim(),
            CategoryId = input.CategoryId,
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

        await PopulateFormAsync(transaction.AccountId, transaction.Type, transaction.CategoryId, cancellationToken);
        return View(new TransactionInputModel
        {
            AccountId = transaction.AccountId,
            RelatedAccountId = transaction.RelatedAccountId,
            Type = transaction.Type,
            Description = transaction.Description,
            CategoryId = transaction.CategoryId,
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
        await ValidateCategoryAsync(input, cancellationToken);
        ValidateAccounts(input, account, destination);

        if (!ModelState.IsValid)
        {
            await PopulateFormAsync(input.AccountId, input.Type, input.CategoryId, cancellationToken);
            return View(input);
        }

        transaction.AccountId = input.AccountId;
        transaction.RelatedAccountId = input.RelatedAccountId;
        transaction.Type = input.Type;
        transaction.Description = input.Description.Trim();
        transaction.CategoryId = input.CategoryId;
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

    private async Task ValidateCategoryAsync(TransactionInputModel input, CancellationToken cancellationToken)
    {
        var expectedType = input.Type switch
        {
            TransactionType.Income or TransactionType.Deposit => TransactionCategoryType.Income,
            TransactionType.Expense or TransactionType.Withdrawal => TransactionCategoryType.Expense,
            TransactionType.Transfer => TransactionCategoryType.Transfer,
            _ => (TransactionCategoryType?)null
        };

        if (!input.CategoryId.HasValue)
        {
            if (input.Type != TransactionType.Transfer)
                ModelState.AddModelError(nameof(input.CategoryId), "Choose a category for this transaction.");
            return;
        }

        var category = await database.TransactionCategories
            .FirstOrDefaultAsync(item => item.Id == input.CategoryId.Value, cancellationToken);
        if (category is null || !category.IsActive)
            ModelState.AddModelError(nameof(input.CategoryId), "Choose an active category.");
        else if (expectedType.HasValue && category.Type != expectedType.Value)
            ModelState.AddModelError(nameof(input.CategoryId), "The selected category does not match the transaction type.");
    }

    private void ValidateAccounts(TransactionInputModel input, Account? account, Account? destination)
    {
        if (account is null || account.Status == AccountStatus.Archived)
            ModelState.AddModelError(nameof(input.AccountId), "Choose an existing, non-archived account.");
        else if (!string.Equals(account.CurrencyCode, input.CurrencyCode?.Trim(), StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(input.CurrencyCode), "Transaction currency must match the selected account currency.");

        if (input.Type == TransactionType.Transfer)
        {
            if (destination is null || destination.Status == AccountStatus.Archived)
                ModelState.AddModelError(nameof(input.RelatedAccountId), "Choose an existing, non-archived destination account.");
            else if (account is not null && account.CurrencyCode != destination.CurrencyCode)
                ModelState.AddModelError(nameof(input.RelatedAccountId), "Transfers currently require matching account currencies.");
        }
    }

    private async Task PopulateFormAsync(int? selectedAccountId, TransactionType? type, int? selectedCategoryId, CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(a => a.Status != AccountStatus.Archived)
            .OrderBy(a => a.Name).ToListAsync(cancellationToken);
        ViewBag.Accounts = new SelectList(accounts, "Id", "Name", selectedAccountId);

        var categoryQuery = database.TransactionCategories.AsNoTracking().Where(category => category.IsActive);
        if (type.HasValue)
        {
            var categoryType = type.Value switch
            {
                TransactionType.Income or TransactionType.Deposit => TransactionCategoryType.Income,
                TransactionType.Expense or TransactionType.Withdrawal => TransactionCategoryType.Expense,
                TransactionType.Transfer => TransactionCategoryType.Transfer,
                _ => (TransactionCategoryType?)null
            };
            if (categoryType.HasValue) categoryQuery = categoryQuery.Where(category => category.Type == categoryType.Value);
        }
        var categories = await categoryQuery.OrderBy(category => category.DisplayOrder).ThenBy(category => category.Name).ToListAsync(cancellationToken);
        ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedCategoryId);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
