using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class CashFlowController(ApplicationDbContext database, CashFlowForecastService forecastService) : Controller
{
    public async Task<IActionResult> Index(int months = 6, CancellationToken cancellationToken = default)
    {
        if (months is < 1 or > 24) return BadRequest();
        var accounts = await database.Accounts.AsNoTracking()
            .Where(x => x.Status == AccountStatus.Active && x.CurrencyCode == "USD"
                && (x.Type == AccountType.Checking || x.Type == AccountType.Savings || x.Type == AccountType.Cash))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToArray();
        var transactions = await database.Transactions.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId) || (x.RelatedAccountId != null && accountIds.Contains(x.RelatedAccountId.Value)))
            .Where(x => x.CurrencyCode == "USD" && x.TransactionDate <= DateTime.Today)
            .ToListAsync(cancellationToken);

        var openingCash = accounts.Sum(account =>
        {
            var balance = account.OpeningBalance;
            foreach (var transaction in transactions)
            {
                if (transaction.AccountId == account.Id)
                {
                    balance += transaction.Type switch
                    {
                        TransactionType.Income or TransactionType.Deposit => transaction.Amount,
                        TransactionType.Expense or TransactionType.Withdrawal => -transaction.Amount,
                        TransactionType.Transfer => -transaction.Amount,
                        _ => 0m
                    };
                }
                if (transaction.RelatedAccountId == account.Id && transaction.Type == TransactionType.Transfer)
                    balance += transaction.Amount;
            }
            return balance;
        });

        var schedules = await database.RecurringCashFlows.AsNoTracking()
            .Include(x => x.Account)
            .Where(x => x.IsActive && accountIds.Contains(x.AccountId))
            .OrderBy(x => x.NextDueDate).ToListAsync(cancellationToken);
        var forecast = forecastService.BuildForecast(schedules, openingCash, DateTime.Today, months);
        var model = new CashFlowIndexViewModel
        {
            Months = months,
            OpeningBalance = openingCash,
            Forecast = forecast,
            Schedules = schedules,
            Accounts = accounts.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSchedule(CashFlowIndexViewModel input, CancellationToken cancellationToken)
    {
        if (input.ScheduleAmount <= 0m || input.ScheduleAmount > 999999999999m)
            ModelState.AddModelError(nameof(input.ScheduleAmount), "Enter an amount greater than zero.");
        if (string.IsNullOrWhiteSpace(input.ScheduleDescription) || input.ScheduleDescription.Length > 160)
            ModelState.AddModelError(nameof(input.ScheduleDescription), "Description is required and must be 160 characters or fewer.");
        if (input.ScheduleType is not (TransactionType.Income or TransactionType.Expense))
            ModelState.AddModelError(nameof(input.ScheduleType), "Only income and expense schedules are supported.");
        var account = await database.Accounts.FirstOrDefaultAsync(x => x.Id == input.ScheduleAccountId
            && x.Status == AccountStatus.Active && x.CurrencyCode == "USD"
            && (x.Type == AccountType.Checking || x.Type == AccountType.Savings || x.Type == AccountType.Cash), cancellationToken);
        if (account is null) ModelState.AddModelError(nameof(input.ScheduleAccountId), "Choose an active USD cash account.");
        if (input.ScheduleEndDate.HasValue && input.ScheduleEndDate.Value.Date < input.ScheduleNextDueDate.Date)
            ModelState.AddModelError(nameof(input.ScheduleEndDate), "End date must be on or after the first due date.");

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "The recurring cash flow could not be saved. Check the entered values.";
            return RedirectToAction(nameof(Index));
        }

        database.RecurringCashFlows.Add(new RecurringCashFlow
        {
            AccountId = account!.Id,
            Type = input.ScheduleType,
            Description = input.ScheduleDescription.Trim(),
            Amount = input.ScheduleAmount,
            CurrencyCode = account.CurrencyCode,
            Frequency = input.ScheduleFrequency,
            NextDueDate = input.ScheduleNextDueDate.Date,
            EndDate = input.ScheduleEndDate?.Date,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Recurring cash flow schedule saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateSchedule(int id, CancellationToken cancellationToken)
    {
        var schedule = await database.RecurringCashFlows.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (schedule is null) return NotFound();
        schedule.IsActive = false;
        schedule.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Recurring schedule deactivated.";
        return RedirectToAction(nameof(Index));
    }
}

public sealed class CashFlowIndexViewModel
{
    public int Months { get; set; } = 6;
    public decimal OpeningBalance { get; set; }
    public IReadOnlyList<CashFlowForecastMonth> Forecast { get; set; } = [];
    public List<RecurringCashFlow> Schedules { get; set; } = [];
    public List<SelectListItem> Accounts { get; set; } = [];
    public int ScheduleAccountId { get; set; }
    public TransactionType ScheduleType { get; set; } = TransactionType.Expense;
    public string ScheduleDescription { get; set; } = string.Empty;
    public decimal ScheduleAmount { get; set; }
    public CashFlowFrequency ScheduleFrequency { get; set; } = CashFlowFrequency.Monthly;
    public DateTime ScheduleNextDueDate { get; set; } = DateTime.Today;
    public DateTime? ScheduleEndDate { get; set; }
}
