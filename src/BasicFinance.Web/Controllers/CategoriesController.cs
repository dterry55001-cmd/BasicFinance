using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class CategoriesController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await database.TransactionCategories.AsNoTracking()
            .Include(category => category.ParentCategory)
            .OrderBy(category => category.Type)
            .ThenBy(category => category.ParentCategoryId)
            .ThenBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);
        ViewBag.Usage = await database.Transactions.AsNoTracking()
            .Where(transaction => transaction.CategoryId != null)
            .GroupBy(transaction => transaction.CategoryId)
            .Select(group => new { CategoryId = group.Key!.Value, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);
        return View(categories);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await PopulateParentsAsync(null, null, cancellationToken);
        return View(new TransactionCategoryInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionCategoryInputModel input, CancellationToken cancellationToken)
    {
        await ValidateCategoryAsync(input, null, cancellationToken);
        if (!ModelState.IsValid)
        {
            await PopulateParentsAsync(input.Type, input.ParentCategoryId, cancellationToken);
            return View(input);
        }

        var now = DateTime.UtcNow;
        database.TransactionCategories.Add(new TransactionCategory
        {
            Name = input.Name.Trim(),
            Type = input.Type,
            ParentCategoryId = input.ParentCategoryId,
            Description = Clean(input.Description),
            DisplayOrder = input.DisplayOrder,
            CreatedUtc = now,
            UpdatedUtc = now
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = await database.TransactionCategories.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (category is null) return NotFound();

        await PopulateParentsAsync(category.Type, category.ParentCategoryId, cancellationToken, category.Id);
        return View(new TransactionCategoryInputModel
        {
            Id = category.Id,
            Name = category.Name,
            Type = category.Type,
            ParentCategoryId = category.ParentCategoryId,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TransactionCategoryInputModel input, CancellationToken cancellationToken)
    {
        var category = await database.TransactionCategories.FindAsync([id], cancellationToken);
        if (category is null) return NotFound();

        await ValidateCategoryAsync(input, id, cancellationToken);
        if (!ModelState.IsValid)
        {
            input.Id = id;
            await PopulateParentsAsync(input.Type, input.ParentCategoryId, cancellationToken, id);
            return View(input);
        }

        category.Name = input.Name.Trim();
        category.Type = input.Type;
        category.ParentCategoryId = input.ParentCategoryId;
        category.Description = Clean(input.Description);
        category.DisplayOrder = input.DisplayOrder;
        category.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var category = await database.TransactionCategories
            .Include(item => item.Children)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (category is null) return NotFound();
        if (category.Children.Any(child => child.IsActive))
        {
            TempData["ErrorMessage"] = "Move or deactivate active subcategories before deactivating this parent category.";
            return RedirectToAction(nameof(Index));
        }

        category.IsActive = false;
        category.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Category deactivated. Existing transactions retain their category history.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        var category = await database.TransactionCategories.FindAsync([id], cancellationToken);
        if (category is null) return NotFound();
        category.IsActive = true;
        category.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Category reactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateCategoryAsync(TransactionCategoryInputModel input, int? currentId, CancellationToken cancellationToken)
    {
        var name = input.Name?.Trim();
        if (!string.IsNullOrWhiteSpace(name) && await database.TransactionCategories.AnyAsync(
            item => item.Id != currentId && item.Type == input.Type && item.ParentCategoryId == input.ParentCategoryId && item.Name.ToLower() == name.ToLower(),
            cancellationToken))
            ModelState.AddModelError(nameof(input.Name), "A category with this name already exists at this level.");

        if (input.ParentCategoryId.HasValue)
        {
            var parent = await database.TransactionCategories.FindAsync([input.ParentCategoryId.Value], cancellationToken);
            if (parent is null || !parent.IsActive)
                ModelState.AddModelError(nameof(input.ParentCategoryId), "Choose an active parent category.");
            else if (parent.Type != input.Type)
                ModelState.AddModelError(nameof(input.ParentCategoryId), "Parent and child categories must have the same type.");
            else if (parent.ParentCategoryId.HasValue)
                ModelState.AddModelError(nameof(input.ParentCategoryId), "Only one subcategory level is supported.");
            else if (currentId == parent.Id)
                ModelState.AddModelError(nameof(input.ParentCategoryId), "A category cannot be its own parent.");
        }

        if (currentId.HasValue)
        {
            var hasTransactions = await database.Transactions.AnyAsync(
                transaction => transaction.CategoryId == currentId.Value, cancellationToken);
            var hasChildren = await database.TransactionCategories.AnyAsync(
                item => item.ParentCategoryId == currentId.Value, cancellationToken);

            if (hasTransactions && await database.TransactionCategories.AnyAsync(
                item => item.Id == currentId.Value && item.Type != input.Type, cancellationToken))
                ModelState.AddModelError(nameof(input.Type), "A category used by transactions cannot change type.");

            if (hasChildren && input.Type != await database.TransactionCategories
                .Where(item => item.Id == currentId.Value)
                .Select(item => item.Type).FirstAsync(cancellationToken))
                ModelState.AddModelError(nameof(input.Type), "A parent category with subcategories cannot change type.");

            if (hasChildren && input.ParentCategoryId.HasValue)
                ModelState.AddModelError(nameof(input.ParentCategoryId), "A category with subcategories cannot become a subcategory.");
        }

        if (currentId.HasValue && await database.TransactionCategories.AnyAsync(
            item => item.ParentCategoryId == currentId && item.Id == input.ParentCategoryId, cancellationToken))
            ModelState.AddModelError(nameof(input.ParentCategoryId), "This parent selection would create a category cycle.");
    }

    private async Task PopulateParentsAsync(TransactionCategoryType? type, int? selectedId, CancellationToken cancellationToken, int? excludeId = null)
    {
        var query = database.TransactionCategories.AsNoTracking().Where(item => item.IsActive && item.ParentCategoryId == null);
        if (type.HasValue) query = query.Where(item => item.Type == type.Value);
        if (excludeId.HasValue) query = query.Where(item => item.Id != excludeId.Value);
        var parents = await query.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Name).ToListAsync(cancellationToken);
        ViewBag.ParentCategories = new SelectList(parents, "Id", "Name", selectedId);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
