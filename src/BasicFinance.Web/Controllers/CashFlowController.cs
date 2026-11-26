using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class CashFlowController(ApplicationDbContext database, CashFlowForecastService forecastService) : Controller
{
    public async Task<IActionResult> Index(string period = "month", int months = 6, int days = 30, int weeks = 12, CancellationToken cancellationToken = default)
    {
        if (months is < 1 or > 24 || days is < 1 or > 90 || weeks is < 1 or > 52
            || period is not ("day" or "week" or "month")) return BadRequest();
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
        var dailyForecast = forecastService.BuildDailyForecast(schedules, openingCash, DateTime.Today, days);
        var weeklyForecast = forecastService.BuildWeeklyForecast(schedules, openingCash, DateTime.Today, weeks);
        var actualVsForecast = forecastService.BuildActualVsForecast(
            schedules, transactions, DateTime.Today.AddMonths(-6), 6);
        var preference = await database.CashFlowAlertPreferences.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var lowBalanceThreshold = preference?.LowBalanceThreshold ?? 250m;
        var projectedBalances = dailyForecast.Select(x => x.ProjectedEndingBalance)
            .Concat(weeklyForecast.Select(x => x.ProjectedEndingBalance))
            .Concat(forecast.Select(x => x.ProjectedEndingBalance)).ToList();
        var projectedPoints = dailyForecast.Select(x => (Date: x.EndDate, Balance: x.ProjectedEndingBalance))
            .Concat(weeklyForecast.Select(x => (Date: x.EndDate, Balance: x.ProjectedEndingBalance)))
            .Concat(forecast.Select(x => (Date: x.Month.AddMonths(1).AddDays(-1), Balance: x.ProjectedEndingBalance)))
            .OrderBy(x => x.Date).ToList();
        var lowestPoint = projectedPoints.OrderBy(x => x.Balance).FirstOrDefault();
        var lowestProjectedBalance = projectedPoints.Count == 0 ? openingCash : lowestPoint.Balance;
        var hasProjectedShortage = projectedPoints.Any(x => x.Balance < 0m);
        var hasLowBalance = projectedPoints.Any(x => x.Balance < lowBalanceThreshold);
        var firstShortagePoint = projectedPoints.Where(x => x.Balance < 0m).OrderBy(x => x.Date).FirstOrDefault();
        var firstLowBalancePoint = projectedPoints.Where(x => x.Balance < lowBalanceThreshold).OrderBy(x => x.Date).FirstOrDefault();
        if ((preference?.AlertsEnabled ?? true) && projectedPoints.Count > 0)
        {
            var alertType = hasProjectedShortage ? CashFlowAlertType.ProjectedShortage
                : hasLowBalance ? CashFlowAlertType.LowBalance : (CashFlowAlertType?)null;
            var alertPoint = hasProjectedShortage ? firstShortagePoint : firstLowBalancePoint;
            if (alertType.HasValue)
            {
                var key = $"{alertType.Value}:{alertPoint.Date:yyyyMMdd}:{lowBalanceThreshold:0.00}";
                var existingAlert = await database.CashFlowAlerts.FirstOrDefaultAsync(x => x.AlertKey == key, cancellationToken);
                if (existingAlert is null)
                {
                    database.CashFlowAlerts.Add(new CashFlowAlert
                    {
                        Type = alertType.Value,
                        ForecastDate = alertPoint.Date,
                        ProjectedBalance = alertPoint.Balance,
                        ThresholdAmount = lowBalanceThreshold,
                        AlertKey = key,
                        Message = alertType == CashFlowAlertType.ProjectedShortage
                            ? $"Projected cash balance first falls below zero on {alertPoint.Date:d}."
                            : $"Projected cash balance first falls below {lowBalanceThreshold:C} on {alertPoint.Date:d}.",
                        CreatedUtc = DateTime.UtcNow
                    });
                    await database.SaveChangesAsync(cancellationToken);
                }
            }
        }
        var alertHistory = await database.CashFlowAlerts.AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc).Take(25).ToListAsync(cancellationToken);
        var model = new CashFlowIndexViewModel
        {
            Months = months,
            Days = days,
            Weeks = weeks,
            Period = period,
            OpeningBalance = openingCash,
            LowBalanceThreshold = lowBalanceThreshold,
            LowestProjectedBalance = lowestProjectedBalance,
            HasLowBalanceWarning = lowestProjectedBalance >= 0m && lowestProjectedBalance < lowBalanceThreshold,
            HasProjectedShortage = lowestProjectedBalance < 0m,
            AlertsEnabled = preference?.AlertsEnabled ?? true,
            AlertHistory = alertHistory,
            Forecast = forecast,
            DailyForecast = dailyForecast,
            WeeklyForecast = weeklyForecast,
            ActualVsForecast = actualVsForecast,
            Schedules = schedules,
            Accounts = accounts.Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToList()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAlertPreference(decimal lowBalanceThreshold, bool alertsEnabled, CancellationToken cancellationToken)
    {
        if (lowBalanceThreshold < 0m || lowBalanceThreshold > 999999999999m)
        {
            TempData["ErrorMessage"] = "Enter a valid non-negative low-balance threshold.";
            return RedirectToAction(nameof(Index));
        }
        var preference = await database.CashFlowAlertPreferences.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (preference is null)
        {
            preference = new CashFlowAlertPreference { Id = 1 };
            database.CashFlowAlertPreferences.Add(preference);
        }
        preference.LowBalanceThreshold = lowBalanceThreshold;
        preference.AlertsEnabled = alertsEnabled;
        preference.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Cash-flow alert settings updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAlertRead(int id, CancellationToken cancellationToken)
    {
        var alert = await database.CashFlowAlerts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (alert is null) return NotFound();
        alert.IsRead = true;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Cash-flow alert marked as read.";
        return RedirectToAction(nameof(Index));
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
    public int Days { get; set; } = 30;
    public int Weeks { get; set; } = 12;
    public string Period { get; set; } = "month";
    public decimal OpeningBalance { get; set; }
    public decimal LowBalanceThreshold { get; set; } = 250m;
    public decimal LowestProjectedBalance { get; set; }
    public bool HasLowBalanceWarning { get; set; }
    public bool HasProjectedShortage { get; set; }
    public bool AlertsEnabled { get; set; } = true;
    public List<CashFlowAlert> AlertHistory { get; set; } = [];
    public IReadOnlyList<CashFlowForecastMonth> Forecast { get; set; } = [];
    public IReadOnlyList<CashFlowForecastPeriod> DailyForecast { get; set; } = [];
    public IReadOnlyList<CashFlowForecastPeriod> WeeklyForecast { get; set; } = [];
    public IReadOnlyList<CashFlowActualVsForecastMonth> ActualVsForecast { get; set; } = [];
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
