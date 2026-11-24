using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class AccountsController(ApplicationDbContext database, AccountBalanceService balances) : Controller
{
    public async Task<IActionResult> Index(bool includeArchived = false)
    {
        var query = database.Accounts.AsNoTracking();
        if (!includeArchived)
            query = query.Where(account => account.Status != AccountStatus.Archived);

        ViewData["IncludeArchived"] = includeArchived;
        var accounts = await query.OrderBy(account => account.Name).ToListAsync();
        ViewData["Balances"] = await Task.WhenAll(accounts.Select(async account =>
            new KeyValuePair<int, decimal>(account.Id, await balances.GetBalanceAsync(account.Id))));
        return View(accounts);
    }

    public async Task<IActionResult> Details(int id)
    {
        var account = await database.Accounts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (account is null) return NotFound();

        ViewBag.CurrentBalance = await balances.GetBalanceAsync(id);
        ViewBag.RecentTransactions = await database.Transactions.AsNoTracking()
            .Where(t => t.AccountId == id || t.RelatedAccountId == id)
            .Include(t => t.Account).Include(t => t.RelatedAccount)
            .OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)
            .Take(10).ToListAsync();
        return View(account);
    }

    public IActionResult Create() => View(new AccountInputModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AccountInputModel input)
    {
        if (!ModelState.IsValid) return View(input);
        var account = new Account
        {
            Name = input.Name.Trim(), Type = input.Type,
            Institution = CleanOptional(input.Institution),
            AccountNumberLast4 = CleanOptional(input.AccountNumberLast4),
            CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant(),
            OpeningBalance = input.OpeningBalance, OpenedOn = input.OpenedOn,
            Status = input.Status, CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow
        };
        database.Accounts.Add(account);
        await database.SaveChangesAsync();
        TempData["SuccessMessage"] = "Account created.";
        return RedirectToAction(nameof(Details), new { id = account.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var account = await database.Accounts.FindAsync(id);
        if (account is null) return NotFound();
        return View(new AccountInputModel
        {
            Name = account.Name, Type = account.Type, Institution = account.Institution,
            AccountNumberLast4 = account.AccountNumberLast4, CurrencyCode = account.CurrencyCode,
            OpeningBalance = account.OpeningBalance, OpenedOn = account.OpenedOn, Status = account.Status
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AccountInputModel input)
    {
        if (!ModelState.IsValid) return View(input);
        var account = await database.Accounts.FindAsync(id);
        if (account is null) return NotFound();
        account.Name = input.Name.Trim(); account.Type = input.Type;
        account.Institution = CleanOptional(input.Institution);
        account.AccountNumberLast4 = CleanOptional(input.AccountNumberLast4);
        account.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        account.OpeningBalance = input.OpeningBalance; account.OpenedOn = input.OpenedOn;
        account.Status = input.Status; account.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();
        TempData["SuccessMessage"] = "Account updated.";
        return RedirectToAction(nameof(Details), new { id = account.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(int id)
    {
        var account = await database.Accounts.FindAsync(id);
        if (account is null) return NotFound();
        account.Status = AccountStatus.Archived;
        account.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync();
        TempData["SuccessMessage"] = "Account archived. Its history remains in the database.";
        return RedirectToAction(nameof(Index), new { includeArchived = true });
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
