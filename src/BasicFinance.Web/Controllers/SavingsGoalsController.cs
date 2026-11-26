using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Controllers;

public sealed class SavingsGoalsController(ApplicationDbContext database, SavingsGoalService savingsService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var goals = await database.SavingsGoals.AsNoTracking()
            .Include(x => x.Contributions)
            .Include(x => x.MonthlyTargets)
            .Include(x => x.Milestones)
            .Where(x => x.Status != SavingsGoalStatus.Archived && x.CurrencyCode == "USD")
            .OrderBy(x => x.Priority).ThenBy(x => x.TargetDate).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var plan = await database.EmergencyFundPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var today = DateTime.Today;
        var cards = goals.Select(goal =>
        {
            var progress = savingsService.CalculateProgress(goal, today);
            var monthlyTarget = goal.MonthlyTargets.FirstOrDefault(x => x.Year == today.Year && x.Month == today.Month);
            var monthlyContributed = savingsService.CalculateMonthlyNetContribution(goal.Contributions, today.Year, today.Month);
            return new SavingsGoalCardViewModel
            {
                Goal = goal,
                Progress = progress,
                MonthlyTarget = monthlyTarget?.TargetAmount ?? 0m,
                MonthlyContributed = monthlyContributed,
                MonthlyRemaining = Math.Max(0m, (monthlyTarget?.TargetAmount ?? 0m) - monthlyContributed)
            };
        }).ToList();

        var emergencyGoal = goals.FirstOrDefault(x => x.IsEmergencyFund);
        var emergencyProgress = emergencyGoal is null ? null : savingsService.CalculateProgress(emergencyGoal, today);
        var monthlyExpenses = plan?.MonthlyEssentialExpenses ?? 0m;
        var coverage = emergencyProgress is null
            ? 0m
            : savingsService.CalculateEmergencyFundCoverageMonths(emergencyProgress.CurrentAmount, monthlyExpenses);

        return View(new SavingsGoalsIndexViewModel
        {
            Goals = cards,
            EmergencyFundPlan = plan,
            EmergencyFundCurrentAmount = emergencyProgress?.CurrentAmount ?? 0m,
            EmergencyFundTargetAmount = emergencyGoal?.TargetAmount ?? (monthlyExpenses * (plan?.TargetCoverageMonths ?? 6)),
            EmergencyFundCoverageMonths = coverage,
            TotalSaved = cards.Sum(x => x.Progress.CurrentAmount),
            TotalTargets = cards.Sum(x => x.Goal.TargetAmount),
            TotalMonthlyTargets = cards.Sum(x => x.MonthlyTarget)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGoal(string name, string? description, decimal targetAmount,
        DateTime startDate, DateTime? targetDate, int priority, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            ModelState.AddModelError(nameof(name), "Enter a goal name of 100 characters or fewer.");
        if (description?.Length > 500)
            ModelState.AddModelError(nameof(description), "Description must be 500 characters or fewer.");
        if (targetAmount <= 0m || targetAmount > 999999999999m)
            ModelState.AddModelError(nameof(targetAmount), "Target amount must be greater than zero.");
        if (priority is < 1 or > 5)
            ModelState.AddModelError(nameof(priority), "Priority must be between 1 and 5.");
        if (targetDate.HasValue && targetDate.Value.Date < startDate.Date)
            ModelState.AddModelError(nameof(targetDate), "Target date must be on or after the start date.");
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "The savings goal could not be created. Check the entered values.";
            return RedirectToAction(nameof(Index));
        }

        database.SavingsGoals.Add(new SavingsGoal
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            TargetAmount = targetAmount,
            CurrencyCode = "USD",
            StartDate = startDate.Date,
            TargetDate = targetDate?.Date,
            Priority = priority,
            Status = SavingsGoalStatus.Active,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Savings goal created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContribution(int goalId, decimal amount, SavingsContributionType type,
        DateTime contributionDate, string? notes, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals
            .Include(x => x.Contributions)
            .Include(x => x.Milestones)
            .FirstOrDefaultAsync(x => x.Id == goalId && x.CurrencyCode == "USD", cancellationToken);
        if (goal is null) return NotFound();
        if (goal.Status != SavingsGoalStatus.Active)
        {
            TempData["ErrorMessage"] = "Contributions can only be added to active goals.";
            return RedirectToAction(nameof(Index));
        }
        if (amount <= 0m || amount > 999999999999m || !Enum.IsDefined(type)
            || contributionDate.Date > DateTime.Today || notes?.Length > 300)
        {
            TempData["ErrorMessage"] = "Enter a valid amount, type, date, and notes (300 characters or fewer).";
            return RedirectToAction(nameof(Index));
        }

        var current = savingsService.CalculateProgress(goal, DateTime.Today).CurrentAmount;
        if (type == SavingsContributionType.Withdrawal && amount > current)
        {
            TempData["ErrorMessage"] = "A withdrawal cannot exceed the goal's current saved amount.";
            return RedirectToAction(nameof(Index));
        }

        var contribution = new SavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = amount,
            Type = type,
            ContributionDate = contributionDate.Date,
            Notes = notes?.Trim(),
            CreatedUtc = DateTime.UtcNow
        };
        database.SavingsContributions.Add(contribution);
        goal.Contributions.Add(contribution);
        goal.UpdatedUtc = DateTime.UtcNow;

        var updatedProgress = savingsService.CalculateProgress(goal, DateTime.Today);
        foreach (var milestone in new[] { 25, 50, 75, 100 })
        {
            if (updatedProgress.CurrentAmount >= goal.TargetAmount * milestone / 100m
                && goal.Milestones.All(x => x.MilestonePercent != milestone))
            {
                database.SavingsGoalMilestones.Add(new SavingsGoalMilestone
                {
                    SavingsGoalId = goal.Id,
                    MilestonePercent = milestone,
                    AchievedUtc = DateTime.UtcNow
                });
            }
        }
        if (updatedProgress.CurrentAmount >= goal.TargetAmount)
            goal.Status = SavingsGoalStatus.Completed;

        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = type == SavingsContributionType.Withdrawal
            ? "Savings withdrawal recorded."
            : "Savings contribution recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMonthlyTarget(int goalId, int year, int month, decimal targetAmount,
        CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12 || targetAmount <= 0m || targetAmount > 999999999999m)
        {
            TempData["ErrorMessage"] = "Enter a valid monthly savings target.";
            return RedirectToAction(nameof(Index));
        }
        var goal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.Id == goalId
            && x.CurrencyCode == "USD" && x.Status != SavingsGoalStatus.Archived, cancellationToken);
        if (goal is null) return NotFound();
        var target = await database.SavingsMonthlyTargets.FirstOrDefaultAsync(x =>
            x.SavingsGoalId == goalId && x.Year == year && x.Month == month, cancellationToken);
        if (target is null)
        {
            database.SavingsMonthlyTargets.Add(new SavingsMonthlyTarget
            {
                SavingsGoalId = goalId, Year = year, Month = month, TargetAmount = targetAmount,
                CreatedUtc = DateTime.UtcNow, UpdatedUtc = DateTime.UtcNow
            });
        }
        else
        {
            target.TargetAmount = targetAmount;
            target.UpdatedUtc = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Monthly savings target saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmergencyFundPlan(decimal monthlyEssentialExpenses, int targetCoverageMonths,
        CancellationToken cancellationToken)
    {
        if (monthlyEssentialExpenses <= 0m || monthlyEssentialExpenses > 999999999999m
            || targetCoverageMonths is < 1 or > 24
            || monthlyEssentialExpenses * targetCoverageMonths > 999999999999m)
        {
            TempData["ErrorMessage"] = "Enter valid essential monthly expenses and a coverage target from 1 to 24 months.";
            return RedirectToAction(nameof(Index));
        }

        var plan = await database.EmergencyFundPlans.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (plan is null)
        {
            plan = new EmergencyFundPlan { Id = 1 };
            database.EmergencyFundPlans.Add(plan);
        }
        plan.MonthlyEssentialExpenses = monthlyEssentialExpenses;
        plan.TargetCoverageMonths = targetCoverageMonths;
        plan.UpdatedUtc = DateTime.UtcNow;

        var emergencyGoal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.IsEmergencyFund, cancellationToken);
        var targetAmount = monthlyEssentialExpenses * targetCoverageMonths;
        if (emergencyGoal is null)
        {
            emergencyGoal = new SavingsGoal
            {
                Name = "Emergency Fund",
                Description = "Cash reserve for essential living expenses.",
                TargetAmount = targetAmount,
                CurrencyCode = "USD",
                StartDate = DateTime.Today,
                Status = SavingsGoalStatus.Active,
                IsEmergencyFund = true,
                Priority = 1,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            database.SavingsGoals.Add(emergencyGoal);
        }
        else
        {
            emergencyGoal.TargetAmount = targetAmount;
            emergencyGoal.Status = SavingsGoalStatus.Active;
            emergencyGoal.UpdatedUtc = DateTime.UtcNow;
        }

        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Emergency fund plan updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveGoal(int id, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (goal is null) return NotFound();
        if (goal.IsEmergencyFund)
        {
            TempData["ErrorMessage"] = "Update the emergency fund plan instead of archiving its goal.";
            return RedirectToAction(nameof(Index));
        }
        goal.Status = SavingsGoalStatus.Archived;
        goal.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Savings goal archived.";
        return RedirectToAction(nameof(Index));
    }
}

public sealed class SavingsGoalsIndexViewModel
{
    public List<SavingsGoalCardViewModel> Goals { get; set; } = [];
    public EmergencyFundPlan? EmergencyFundPlan { get; set; }
    public decimal EmergencyFundCurrentAmount { get; set; }
    public decimal EmergencyFundTargetAmount { get; set; }
    public decimal EmergencyFundCoverageMonths { get; set; }
    public decimal TotalSaved { get; set; }
    public decimal TotalTargets { get; set; }
    public decimal TotalMonthlyTargets { get; set; }
}

public sealed class SavingsGoalCardViewModel
{
    public SavingsGoal Goal { get; set; } = null!;
    public SavingsGoalProgress Progress { get; set; } = null!;
    public decimal MonthlyTarget { get; set; }
    public decimal MonthlyContributed { get; set; }
    public decimal MonthlyRemaining { get; set; }
}
