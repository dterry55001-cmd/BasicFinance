using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class RulesController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var rules = await database.TransactionRules.AsNoTracking()
            .Include(rule => rule.Account)
            .Include(rule => rule.TransactionCategory)
            .OrderBy(rule => rule.Priority).ThenBy(rule => rule.Name)
            .ToListAsync(cancellationToken);
        return View(rules);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateOptionsAsync(null, null, cancellationToken);
        return View(new TransactionRuleInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionRuleInputModel input, CancellationToken cancellationToken)
    {
        await ValidateAsync(input, null, cancellationToken);
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(input.AccountId, input.TransactionCategoryId, cancellationToken);
            return View(input);
        }

        var now = DateTime.UtcNow;
        database.TransactionRules.Add(new TransactionRule
        {
            Name = input.Name.Trim(),
            MatchText = input.MatchText.Trim(),
            MatchType = input.MatchType,
            AccountId = input.AccountId,
            TransactionType = input.TransactionType,
            TransactionCategoryId = input.TransactionCategoryId,
            Priority = input.Priority,
            IsActive = true,
            CreatedUtc = now,
            UpdatedUtc = now
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Transaction rule created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var rule = await database.TransactionRules.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rule is null) return NotFound();
        await PopulateOptionsAsync(rule.AccountId, rule.TransactionCategoryId, cancellationToken);
        return View(new TransactionRuleInputModel
        {
            Id = rule.Id,
            Name = rule.Name,
            MatchText = rule.MatchText,
            MatchType = rule.MatchType,
            AccountId = rule.AccountId,
            TransactionType = rule.TransactionType,
            TransactionCategoryId = rule.TransactionCategoryId,
            Priority = rule.Priority
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TransactionRuleInputModel input, CancellationToken cancellationToken)
    {
        var rule = await database.TransactionRules.FindAsync([id], cancellationToken);
        if (rule is null) return NotFound();
        await ValidateAsync(input, id, cancellationToken);
        if (!ModelState.IsValid)
        {
            input.Id = id;
            await PopulateOptionsAsync(input.AccountId, input.TransactionCategoryId, cancellationToken);
            return View(input);
        }

        rule.Name = input.Name.Trim();
        rule.MatchText = input.MatchText.Trim();
        rule.MatchType = input.MatchType;
        rule.AccountId = input.AccountId;
        rule.TransactionType = input.TransactionType;
        rule.TransactionCategoryId = input.TransactionCategoryId;
        rule.Priority = input.Priority;
        rule.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Transaction rule updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken cancellationToken)
    {
        var rule = await database.TransactionRules.FindAsync([id], cancellationToken);
        if (rule is null) return NotFound();
        rule.IsActive = !rule.IsActive;
        rule.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = rule.IsActive ? "Rule activated." : "Rule deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateAsync(TransactionRuleInputModel input, int? currentId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(input.MatchType))
            ModelState.AddModelError(nameof(input.MatchType), "Choose a supported match method.");
        if (input.TransactionType.HasValue && !Enum.IsDefined(input.TransactionType.Value))
            ModelState.AddModelError(nameof(input.TransactionType), "Choose a supported transaction type.");

        if (input.AccountId.HasValue && !await database.Accounts.AnyAsync(
            account => account.Id == input.AccountId.Value && account.Status != AccountStatus.Archived, cancellationToken))
            ModelState.AddModelError(nameof(input.AccountId), "Choose an active account.");

        var category = await database.TransactionCategories.FirstOrDefaultAsync(
            item => item.Id == input.TransactionCategoryId && item.IsActive, cancellationToken);
        if (category is null)
            ModelState.AddModelError(nameof(input.TransactionCategoryId), "Choose an active category.");
        else if (input.TransactionType.HasValue && !CategoryMatches(category.Type, input.TransactionType.Value))
            ModelState.AddModelError(nameof(input.TransactionCategoryId), "The category does not match the selected transaction type.");

        var normalizedName = input.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedName) && await database.TransactionRules.AnyAsync(rule =>
            rule.Id != currentId && rule.Name.ToLower() == normalizedName.ToLower(), cancellationToken))
            ModelState.AddModelError(nameof(input.Name), "A rule with this name already exists.");
    }

    private async Task PopulateOptionsAsync(int? accountId, int? categoryId, CancellationToken cancellationToken)
    {
        var accounts = await database.Accounts.AsNoTracking()
            .Where(account => account.Status != AccountStatus.Archived)
            .OrderBy(account => account.Name).ToListAsync(cancellationToken);
        ViewBag.Accounts = new SelectList(accounts, "Id", "Name", accountId);

        var categories = await database.TransactionCategories.AsNoTracking()
            .Include(category => category.ParentCategory)
            .Where(category => category.IsActive)
            .OrderBy(category => category.Type).ThenBy(category => category.ParentCategoryId)
            .ThenBy(category => category.DisplayOrder).ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);
        ViewBag.Categories = new SelectList(categories.Select(category => new
        {
            Id = category.Id,
            Name = category.ParentCategory == null ? $"{category.Type}: {category.Name}" : $"{category.Type}: {category.ParentCategory.Name} — {category.Name}"
        }), "Id", "Name", categoryId);
    }

    private static bool CategoryMatches(TransactionCategoryType categoryType, TransactionType transactionType) => categoryType switch
    {
        TransactionCategoryType.Income => transactionType is TransactionType.Income or TransactionType.Deposit,
        TransactionCategoryType.Expense => transactionType is TransactionType.Expense or TransactionType.Withdrawal,
        TransactionCategoryType.Transfer => transactionType == TransactionType.Transfer,
        _ => false
    };
}
