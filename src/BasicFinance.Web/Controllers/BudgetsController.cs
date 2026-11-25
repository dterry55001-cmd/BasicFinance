using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class BudgetsController(ApplicationDbContext database) : Controller
{
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken cancellationToken)
    {
        var selectedYear = year ?? DateTime.Today.Year;
        var selectedMonth = month ?? DateTime.Today.Month;
        if (selectedMonth is < 1 or > 12) return BadRequest();

        var period = await database.BudgetPeriods.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.Category)
            .FirstOrDefaultAsync(x => x.Year == selectedYear && x.Month == selectedMonth && x.CurrencyCode == "USD", cancellationToken);

        var categories = await database.TransactionCategories.AsNoTracking()
            .Where(x => x.Type == TransactionCategoryType.Expense && x.IsActive)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken);

        var start = new DateTime(selectedYear, selectedMonth, 1);
        var end = start.AddMonths(1);
        var actuals = await database.Transactions.AsNoTracking()
            .Where(x => x.Type == TransactionType.Expense && x.CurrencyCode == "USD"
                && x.TransactionDate >= start && x.TransactionDate < end && x.CategoryId != null)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Amount, cancellationToken);

        var lines = categories.Select(category =>
        {
            var line = period?.Lines.FirstOrDefault(x => x.CategoryId == category.Id);
            actuals.TryGetValue(category.Id, out var actual);
            return new BudgetLineViewModel
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                PlannedAmount = line?.PlannedAmount ?? 0m,
                RolloverAmount = line?.RolloverAmount ?? 0m,
                ActualAmount = actual,
                Notes = line?.Notes
            };
        }).ToList();

        var preferences = await database.BudgetAlertPreferences.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var alertsEnabled = preferences?.IsEnabled ?? true;
        if (period is not null && alertsEnabled)
        {
            var existingThresholds = await database.BudgetAlerts.AsNoTracking()
                .Where(x => x.BudgetPeriodId == period.Id)
                .Select(x => new { x.CategoryId, x.ThresholdPercent })
                .ToListAsync(cancellationToken);
            var thresholds = new[] { 50, 75, 90, 100 };
            foreach (var line in lines.Where(x => x.PlannedAmount > 0m))
            {
                var alertBudget = line.AdjustedBudget > 0m ? line.AdjustedBudget : line.PlannedAmount;
                var utilization = line.ActualAmount / alertBudget * 100m;
                foreach (var threshold in thresholds.Where(x => utilization >= x))
                {
                    if (existingThresholds.Any(x => x.CategoryId == line.CategoryId && x.ThresholdPercent == threshold))
                        continue;
                    var level = threshold >= 100 ? "Critical" : threshold >= 90 ? "High" : threshold >= 75 ? "Warning" : "Information";
                    database.BudgetAlerts.Add(new BudgetAlert
                    {
                        BudgetPeriodId = period.Id,
                        CategoryId = line.CategoryId,
                        ThresholdPercent = threshold,
                        ActualAmount = line.ActualAmount,
                        Level = level,
                        Message = $"{line.CategoryName} has reached {utilization:0.#}% of its {alertBudget:C} adjusted budget.",
                        CreatedUtc = DateTime.UtcNow
                    });
                }
            }
            await database.SaveChangesAsync(cancellationToken);
        }

        var alerts = period is null
            ? new List<BudgetAlert>()
            : await database.BudgetAlerts.AsNoTracking().Include(x => x.Category)
                .Where(x => x.BudgetPeriodId == period.Id)
                .OrderByDescending(x => x.CreatedUtc).Take(20).ToListAsync(cancellationToken);

        return View(new BudgetIndexViewModel
        {
            Year = selectedYear,
            Month = selectedMonth,
            PeriodName = period?.Name ?? start.ToString("MMMM yyyy"),
            Lines = lines,
            Alerts = alerts,
            AlertsEnabled = alertsEnabled,
            IsRecurring = period?.IsRecurring ?? false,
            RolloverEnabled = period?.RolloverEnabled ?? false
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyPreviousMonth(int year, int month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2200) return BadRequest();
        var currentStart = new DateTime(year, month, 1);
        var previousStart = currentStart.AddMonths(-1);
        var current = await database.BudgetPeriods
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month && x.CurrencyCode == "USD", cancellationToken);
        if (current is not null)
        {
            TempData["SuccessMessage"] = "A budget already exists for this month. Edit it directly instead of copying over existing allocations.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        var previous = await database.BudgetPeriods.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Year == previousStart.Year && x.Month == previousStart.Month && x.CurrencyCode == "USD", cancellationToken);
        if (previous is null)
        {
            TempData["SuccessMessage"] = "There is no previous-month budget to copy.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        var previousEnd = currentStart;
        var actuals = await database.Transactions.AsNoTracking()
            .Where(x => x.Type == TransactionType.Expense && x.CurrencyCode == "USD"
                && x.TransactionDate >= previousStart && x.TransactionDate < previousEnd && x.CategoryId != null)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Amount, cancellationToken);

        var target = new BudgetPeriod
        {
            Name = currentStart.ToString("MMMM yyyy"),
            Year = year,
            Month = month,
            CurrencyCode = "USD",
            IsRecurring = previous.IsRecurring,
            RolloverEnabled = previous.RolloverEnabled,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        foreach (var line in previous.Lines)
        {
            actuals.TryGetValue(line.CategoryId, out var previousActual);
            var rollover = BudgetRolloverCalculator.CalculateNextMonth(
                line.PlannedAmount, line.RolloverAmount, previousActual, previous.RolloverEnabled);
            target.Lines.Add(new BudgetLine
            {
                CategoryId = line.CategoryId,
                PlannedAmount = line.PlannedAmount,
                RolloverAmount = rollover,
                Notes = line.Notes,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }

        database.BudgetPeriods.Add(target);
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = previous.IsRecurring
            ? "Recurring budget created from the previous month."
            : "Previous month's budget copied successfully.";
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAlertRead(int id, int year, int month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2200) return BadRequest();
        var alert = await database.BudgetAlerts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (alert is null) return NotFound();
        alert.IsRead = true;
        await database.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAlertsRead(int year, int month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2200) return BadRequest();
        var period = await database.BudgetPeriods.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month && x.CurrencyCode == "USD", cancellationToken);
        if (period is not null)
            await database.BudgetAlerts.Where(x => x.BudgetPeriodId == period.Id && !x.IsRead)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true), cancellationToken);
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAlertSettings(bool isEnabled, int year, int month, CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2200) return BadRequest();
        var preferences = await database.BudgetAlertPreferences.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (preferences is null)
        {
            preferences = new BudgetAlertPreference { Id = 1, IsEnabled = isEnabled, UpdatedUtc = DateTime.UtcNow };
            database.BudgetAlertPreferences.Add(preferences);
        }
        else
        {
            preferences.IsEnabled = isEnabled;
            preferences.UpdatedUtc = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = isEnabled ? "Budget alerts enabled." : "Budget alerts disabled.";
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BudgetIndexViewModel input, CancellationToken cancellationToken)
    {
        if (input.Month is < 1 or > 12 || input.Year is < 2000 or > 2200) return BadRequest();
        if (input.Lines.Any(x => x.PlannedAmount < 0 || x.PlannedAmount > 999999999999m))
            ModelState.AddModelError(nameof(input.Lines), "Budget amounts must be zero or greater.");

        var categoryIds = input.Lines.Select(x => x.CategoryId).Distinct().ToArray();
        var validIds = await database.TransactionCategories.AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id) && x.Type == TransactionCategoryType.Expense && x.IsActive)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        if (validIds.Count != categoryIds.Length)
            ModelState.AddModelError(nameof(input.Lines), "One or more expense categories are invalid or inactive.");

        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Index), new { year = input.Year, month = input.Month });

        var period = await database.BudgetPeriods
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Year == input.Year && x.Month == input.Month && x.CurrencyCode == "USD", cancellationToken);

        if (period is null)
        {
            var date = new DateTime(input.Year, input.Month, 1);
            period = new BudgetPeriod { Name = date.ToString("MMMM yyyy"), Year = input.Year, Month = input.Month, CurrencyCode = "USD", IsRecurring = input.IsRecurring, RolloverEnabled = input.RolloverEnabled };
            database.BudgetPeriods.Add(period);
        }

        var now = DateTime.UtcNow;
        period.IsRecurring = input.IsRecurring;
        period.RolloverEnabled = input.RolloverEnabled;
        foreach (var item in input.Lines)
        {
            var existing = period.Lines.FirstOrDefault(x => x.CategoryId == item.CategoryId);
            if (existing is null)
            {
                period.Lines.Add(new BudgetLine { CategoryId = item.CategoryId, PlannedAmount = item.PlannedAmount, Notes = item.Notes?.Trim(), CreatedUtc = now, UpdatedUtc = now });
            }
            else
            {
                existing.PlannedAmount = item.PlannedAmount;
                existing.Notes = item.Notes?.Trim();
                existing.UpdatedUtc = now;
            }
        }

        period.UpdatedUtc = now;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Budget saved.";
        return RedirectToAction(nameof(Index), new { year = input.Year, month = input.Month });
    }
}

public sealed class BudgetIndexViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public List<BudgetLineViewModel> Lines { get; set; } = [];
    public List<BudgetAlert> Alerts { get; set; } = [];
    public bool AlertsEnabled { get; set; } = true;
    public bool IsRecurring { get; set; }
    public bool RolloverEnabled { get; set; }
    public decimal TotalPlanned => Lines.Sum(x => x.PlannedAmount);
    public decimal TotalActual => Lines.Sum(x => x.ActualAmount);
}

public sealed class BudgetLineViewModel
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal PlannedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal RolloverAmount { get; set; }
    public decimal AdjustedBudget => PlannedAmount + RolloverAmount;
    public string? Notes { get; set; }
    public decimal Remaining => AdjustedBudget - ActualAmount;
}
