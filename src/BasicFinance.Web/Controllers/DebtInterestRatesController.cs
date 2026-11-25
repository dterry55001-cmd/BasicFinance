using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class DebtInterestRatesController(ApplicationDbContext database) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int accountId, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.Status == AccountStatus.Active &&
                (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard), cancellationToken);
        if (account is null) return NotFound();
        ViewBag.Account = account;
        return View(await database.DebtInterestRateHistory.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(DebtInterestRateHistory input, CancellationToken cancellationToken)
    {
        var account = await database.Accounts.FirstOrDefaultAsync(a => a.Id == input.AccountId &&
            a.Status == AccountStatus.Active && (a.Type == AccountType.Debt || a.Type == AccountType.CreditCard), cancellationToken);
        if (account is null) return NotFound();
        input.EffectiveDate = input.EffectiveDate.Date;
        if (input.PromotionalEndDate.HasValue && input.PromotionalEndDate.Value.Date < input.EffectiveDate)
            ModelState.AddModelError(nameof(input.PromotionalEndDate), "Promotional end date cannot be before the effective date.");
        if (!ModelState.IsValid) {
            TempData["ErrorMessage"] = "Check the APR and effective dates and try again.";
            return RedirectToAction(nameof(Index), new { accountId = input.AccountId });
        }
        database.DebtInterestRateHistory.Add(new DebtInterestRateHistory {
            AccountId = input.AccountId, AnnualRatePercent = input.AnnualRatePercent,
            EffectiveDate = input.EffectiveDate, RateType = input.RateType.Trim(),
            PromotionalEndDate = input.PromotionalEndDate?.Date,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "APR history entry saved. Existing account APR was not overwritten.";
        return RedirectToAction(nameof(Index), new { accountId = input.AccountId });
    }
}
