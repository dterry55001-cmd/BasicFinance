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
            .Include(x => x.Contributions).ThenInclude(x => x.Transaction)
            .Include(x => x.MonthlyTargets)
            .Include(x => x.Milestones)
            .Include(x => x.RecurringContributions)
            .Include(x => x.AllocationSettings)
            .Where(x => x.Status != SavingsGoalStatus.Archived && x.CurrencyCode == "USD")
            .OrderBy(x => x.Priority).ThenBy(x => x.TargetDate).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var plan = await database.EmergencyFundPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var allocationBudget = await database.SavingsAllocationBudgets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var today = DateTime.Today;
        var availableTransactions = await database.Transactions.AsNoTracking()
            .Where(x => x.CurrencyCode == "USD"
                && x.TransactionDate <= today
                && (x.Type == TransactionType.Income || x.Type == TransactionType.Deposit
                    || x.Type == TransactionType.Expense || x.Type == TransactionType.Withdrawal)
                && !database.SavingsContributions.Any(contribution => contribution.TransactionId == x.Id))
            .OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id)
            .Take(100).ToListAsync(cancellationToken);
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
                MonthlyRemaining = Math.Max(0m, (monthlyTarget?.TargetAmount ?? 0m) - monthlyContributed),
                AllocationPercent = goal.AllocationSettings.FirstOrDefault()?.AllocationPercent ?? 0m,
                PlannedAllocation = savingsService.CalculateAllocationAmount(
                    allocationBudget?.MonthlyBudget ?? 0m,
                    goal.AllocationSettings.FirstOrDefault()?.AllocationPercent ?? 0m)
            };
        }).ToList();

        var fundingRecommendations = cards
            .Where(x => x.Goal.Status == SavingsGoalStatus.Active && x.Goal.TargetDate.HasValue
                && x.Progress.RemainingAmount > 0m)
            .Select(x =>
            {
                var required = savingsService.CalculateRequiredMonthlyContribution(
                    x.Progress.RemainingAmount, x.Goal.TargetDate, today) ?? 0m;
                return new SavingsFundingRecommendationViewModel
                {
                    GoalName = x.Goal.Name,
                    Priority = x.Goal.Priority,
                    TargetDate = x.Goal.TargetDate!.Value,
                    RemainingAmount = x.Progress.RemainingAmount,
                    RequiredMonthlyContribution = required,
                    PlannedMonthlyAllocation = x.PlannedAllocation,
                    AdditionalMonthlyAmountNeeded = Math.Max(0m, required - x.PlannedAllocation)
                };
            })
            .OrderBy(x => x.Priority).ThenBy(x => x.TargetDate).ToList();

        var performanceStart = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
        var activity = goals.SelectMany(x => x.Contributions)
            .Where(x => x.ContributionDate.Date >= performanceStart && x.ContributionDate.Date <= today)
            .ToList();
        var monthlyPerformance = Enumerable.Range(0, 6).Select(offset =>
        {
            var month = performanceStart.AddMonths(offset);
            var monthEntries = activity.Where(x => x.ContributionDate.Year == month.Year && x.ContributionDate.Month == month.Month).ToList();
            var contributions = monthEntries.Where(x => x.Type == SavingsContributionType.Contribution).Sum(x => x.Amount);
            var withdrawals = monthEntries.Where(x => x.Type == SavingsContributionType.Withdrawal).Sum(x => x.Amount);
            return new SavingsMonthlyPerformanceViewModel
            {
                Month = month,
                Contributions = contributions,
                Withdrawals = withdrawals,
                NetSavings = contributions - withdrawals
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
            TotalMonthlyTargets = cards.Sum(x => x.MonthlyTarget),
            MonthlyPerformance = monthlyPerformance,
            AvailableTransactions = availableTransactions,
            AllocationBudget = allocationBudget,
            TotalAllocationPercent = cards.Sum(x => x.AllocationPercent),
            TotalPlannedAllocation = cards.Sum(x => x.PlannedAllocation),
            FundingRecommendations = fundingRecommendations
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
        DateTime contributionDate, string? notes, int? transactionId, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals
            .Include(x => x.Contributions)
            .Include(x => x.Milestones)
            .FirstOrDefaultAsync(x => x.Id == goalId && x.CurrencyCode == "USD", cancellationToken);
        if (goal is null) return NotFound();
        if (goal.Status is SavingsGoalStatus.Paused or SavingsGoalStatus.Archived)
        {
            TempData["ErrorMessage"] = "Savings activity cannot be recorded for paused or archived goals.";
            return RedirectToAction(nameof(Index));
        }
        if (amount <= 0m || amount > 999999999999m || !Enum.IsDefined(type)
            || contributionDate.Date > DateTime.Today || contributionDate.Date < goal.StartDate.Date
            || notes?.Length > 300)
        {
            TempData["ErrorMessage"] = "Enter a valid amount, type, date, and notes (300 characters or fewer).";
            return RedirectToAction(nameof(Index));
        }

        FinancialTransaction? linkedTransaction = null;
        if (transactionId.HasValue)
        {
            linkedTransaction = await database.Transactions.FirstOrDefaultAsync(x => x.Id == transactionId.Value, cancellationToken);
            if (!savingsService.IsCompatibleTransaction(linkedTransaction, type, amount, contributionDate, goal.CurrencyCode)
                || await database.SavingsContributions.AnyAsync(x => x.TransactionId == transactionId.Value, cancellationToken))
            {
                TempData["ErrorMessage"] = "The selected transaction must be unlinked, match the contribution type, amount, date, and currency.";
                return RedirectToAction(nameof(Index));
            }
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
            TransactionId = linkedTransaction?.Id,
            Transaction = linkedTransaction,
            Notes = notes?.Trim(),
            CreatedUtc = DateTime.UtcNow
        };
        database.SavingsContributions.Add(contribution);
        goal.Contributions.Add(contribution);
        goal.UpdatedUtc = DateTime.UtcNow;

        var updatedProgress = savingsService.CalculateProgress(goal, DateTime.Today);
        var reachedMilestones = new List<int>();
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
                reachedMilestones.Add(milestone);
            }
        }
        goal.Status = updatedProgress.CurrentAmount >= goal.TargetAmount
            ? SavingsGoalStatus.Completed : SavingsGoalStatus.Active;

        await database.SaveChangesAsync(cancellationToken);
        var activityMessage = type == SavingsContributionType.Withdrawal
            ? "Savings withdrawal recorded."
            : "Savings contribution recorded.";
        TempData["SuccessMessage"] = reachedMilestones.Count > 0
            ? $"{activityMessage} Milestones achieved: {string.Join(", ", reachedMilestones)}%."
            : activityMessage;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRecurringSchedule(int goalId, decimal amount,
        SavingsScheduleFrequency frequency, DateTime nextDueDate, DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.Id == goalId
            && x.CurrencyCode == "USD" && x.Status == SavingsGoalStatus.Active, cancellationToken);
        if (goal is null) return NotFound();
        if (amount <= 0m || amount > 999999999999m || !Enum.IsDefined(frequency)
            || nextDueDate.Date < DateTime.Today || nextDueDate.Date < goal.StartDate.Date
            || (endDate.HasValue && endDate.Value.Date < nextDueDate.Date))
        {
            TempData["ErrorMessage"] = "Enter a valid recurring amount, frequency, and due-date range.";
            return RedirectToAction(nameof(Index));
        }

        database.RecurringSavingsContributions.Add(new RecurringSavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = amount,
            Frequency = frequency,
            NextDueDate = nextDueDate.Date,
            EndDate = endDate?.Date,
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Recurring savings schedule created. Due contributions will await your confirmation.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordScheduledContribution(int scheduleId, CancellationToken cancellationToken)
    {
        var schedule = await database.RecurringSavingsContributions
            .Include(x => x.SavingsGoal).ThenInclude(x => x.Contributions)
            .Include(x => x.SavingsGoal).ThenInclude(x => x.Milestones)
            .FirstOrDefaultAsync(x => x.Id == scheduleId && x.IsActive, cancellationToken);
        if (schedule is null) return NotFound();
        var today = DateTime.Today;
        if (schedule.SavingsGoal.Status != SavingsGoalStatus.Active || schedule.NextDueDate.Date > today
            || (schedule.EndDate.HasValue && schedule.NextDueDate.Date > schedule.EndDate.Value.Date))
        {
            TempData["ErrorMessage"] = "This scheduled contribution is not currently due or its goal is inactive.";
            return RedirectToAction(nameof(Index));
        }

        var goal = schedule.SavingsGoal;
        if (await database.SavingsContributions.AnyAsync(x =>
            x.RecurringSavingsContributionId == schedule.Id && x.ScheduledDueDate == schedule.NextDueDate.Date,
            cancellationToken))
        {
            TempData["ErrorMessage"] = "This scheduled occurrence has already been recorded.";
            return RedirectToAction(nameof(Index));
        }
        var contribution = new SavingsContribution
        {
            SavingsGoalId = goal.Id,
            Amount = schedule.Amount,
            Type = SavingsContributionType.Contribution,
            ContributionDate = schedule.NextDueDate.Date,
            RecurringSavingsContributionId = schedule.Id,
            ScheduledDueDate = schedule.NextDueDate.Date,
            Notes = $"Recurring schedule #{schedule.Id}",
            CreatedUtc = DateTime.UtcNow
        };
        database.SavingsContributions.Add(contribution);
        goal.Contributions.Add(contribution);
        goal.UpdatedUtc = DateTime.UtcNow;
        var progress = savingsService.CalculateProgress(goal, today);
        foreach (var milestone in new[] { 25, 50, 75, 100 })
        {
            if (progress.CurrentAmount >= goal.TargetAmount * milestone / 100m
                && goal.Milestones.All(x => x.MilestonePercent != milestone))
            {
                database.SavingsGoalMilestones.Add(new SavingsGoalMilestone
                {
                    SavingsGoalId = goal.Id, MilestonePercent = milestone, AchievedUtc = DateTime.UtcNow
                });
            }
        }
        goal.Status = progress.CurrentAmount >= goal.TargetAmount
            ? SavingsGoalStatus.Completed : SavingsGoalStatus.Active;
        var nextDueDate = savingsService.GetNextSavingsDueDate(schedule.NextDueDate, schedule.Frequency);
        if (goal.Status == SavingsGoalStatus.Completed)
            schedule.IsActive = false;
        if (schedule.EndDate.HasValue && nextDueDate.Date > schedule.EndDate.Value.Date)
            schedule.IsActive = false;
        else
            schedule.NextDueDate = nextDueDate;
        schedule.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"Scheduled savings contribution of {schedule.Amount:C} recorded.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateRecurringSchedule(int id, CancellationToken cancellationToken)
    {
        var schedule = await database.RecurringSavingsContributions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (schedule is null) return NotFound();
        schedule.IsActive = false;
        schedule.UpdatedUtc = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Recurring savings schedule deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAllocationPlan(decimal monthlyBudget, CancellationToken cancellationToken)
    {
        if (monthlyBudget < 0m || monthlyBudget > 999999999999m)
        {
            TempData["ErrorMessage"] = "Enter a valid monthly savings allocation budget.";
            return RedirectToAction(nameof(Index));
        }
        var budget = await database.SavingsAllocationBudgets.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (budget is null)
        {
            budget = new SavingsAllocationBudget { Id = 1, MonthlyBudget = monthlyBudget, UpdatedUtc = DateTime.UtcNow };
            database.SavingsAllocationBudgets.Add(budget);
        }
        else
        {
            budget.MonthlyBudget = monthlyBudget;
            budget.UpdatedUtc = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Monthly savings allocation budget saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGoalAllocation(int goalId, decimal allocationPercent, CancellationToken cancellationToken)
    {
        if (allocationPercent is < 0m or > 100m)
        {
            TempData["ErrorMessage"] = "Allocation must be between 0% and 100%.";
            return RedirectToAction(nameof(Index));
        }
        var goal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.Id == goalId
            && x.CurrencyCode == "USD" && x.Status != SavingsGoalStatus.Archived, cancellationToken);
        if (goal is null) return NotFound();
        var settings = await database.SavingsAllocationSettings
            .Where(x => x.SavingsGoalId != goalId && x.SavingsGoal.Status != SavingsGoalStatus.Archived
                && x.SavingsGoal.CurrencyCode == "USD")
            .Select(x => x.AllocationPercent).ToListAsync(cancellationToken);
        if (!savingsService.IsAllocationTotalValid(settings, allocationPercent))
        {
            TempData["ErrorMessage"] = "Goal allocations cannot total more than 100%.";
            return RedirectToAction(nameof(Index));
        }
        var setting = await database.SavingsAllocationSettings.FirstOrDefaultAsync(x => x.SavingsGoalId == goalId, cancellationToken);
        if (setting is null)
        {
            database.SavingsAllocationSettings.Add(new SavingsAllocationSetting
            {
                SavingsGoalId = goalId, AllocationPercent = allocationPercent, UpdatedUtc = DateTime.UtcNow
            });
        }
        else
        {
            setting.AllocationPercent = allocationPercent;
            setting.UpdatedUtc = DateTime.UtcNow;
        }
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Goal allocation updated.";
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

        var emergencyGoal = await database.SavingsGoals.Include(x => x.Contributions)
            .FirstOrDefaultAsync(x => x.IsEmergencyFund, cancellationToken);
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

        var emergencyProgress = savingsService.CalculateProgress(emergencyGoal, DateTime.Today);
        emergencyGoal.Status = emergencyProgress.CurrentAmount >= targetAmount
            ? SavingsGoalStatus.Completed : SavingsGoalStatus.Active;
        emergencyGoal.UpdatedUtc = DateTime.UtcNow;

        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Emergency fund plan updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGoal(int id, string name, string? description, decimal targetAmount,
        DateTime startDate, DateTime? targetDate, int priority, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals.Include(x => x.Contributions)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsEmergencyFund && x.Status != SavingsGoalStatus.Archived, cancellationToken);
        if (goal is null) return NotFound();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || description?.Length > 500
            || targetAmount <= 0m || targetAmount > 999999999999m || priority is < 1 or > 5
            || (targetDate.HasValue && targetDate.Value.Date < startDate.Date))
        {
            TempData["ErrorMessage"] = "The savings goal changes are invalid.";
            return RedirectToAction(nameof(Index));
        }

        goal.Name = name.Trim();
        goal.Description = description?.Trim();
        goal.TargetAmount = targetAmount;
        goal.StartDate = startDate.Date;
        goal.TargetDate = targetDate?.Date;
        goal.Priority = priority;
        goal.UpdatedUtc = DateTime.UtcNow;
        var progress = savingsService.CalculateProgress(goal, DateTime.Today);
        if (goal.Status != SavingsGoalStatus.Paused)
            goal.Status = progress.CurrentAmount >= goal.TargetAmount ? SavingsGoalStatus.Completed : SavingsGoalStatus.Active;
        await database.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Savings goal updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PauseGoal(int id, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals.FirstOrDefaultAsync(x => x.Id == id && !x.IsEmergencyFund, cancellationToken);
        if (goal is null) return NotFound();
        if (goal.Status == SavingsGoalStatus.Active)
        {
            goal.Status = SavingsGoalStatus.Paused;
            goal.UpdatedUtc = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeGoal(int id, CancellationToken cancellationToken)
    {
        var goal = await database.SavingsGoals.Include(x => x.Contributions)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsEmergencyFund, cancellationToken);
        if (goal is null) return NotFound();
        if (goal.Status == SavingsGoalStatus.Paused)
        {
            var progress = savingsService.CalculateProgress(goal, DateTime.Today);
            goal.Status = progress.CurrentAmount >= goal.TargetAmount ? SavingsGoalStatus.Completed : SavingsGoalStatus.Active;
            goal.UpdatedUtc = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }
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
    public List<SavingsMonthlyPerformanceViewModel> MonthlyPerformance { get; set; } = [];
    public List<FinancialTransaction> AvailableTransactions { get; set; } = [];
    public SavingsAllocationBudget? AllocationBudget { get; set; }
    public decimal TotalAllocationPercent { get; set; }
    public decimal TotalPlannedAllocation { get; set; }
    public List<SavingsFundingRecommendationViewModel> FundingRecommendations { get; set; } = [];
}

public sealed class SavingsMonthlyPerformanceViewModel
{
    public DateTime Month { get; set; }
    public decimal Contributions { get; set; }
    public decimal Withdrawals { get; set; }
    public decimal NetSavings { get; set; }
}

public sealed class SavingsGoalCardViewModel
{
    public SavingsGoal Goal { get; set; } = null!;
    public SavingsGoalProgress Progress { get; set; } = null!;
    public decimal MonthlyTarget { get; set; }
    public decimal MonthlyContributed { get; set; }
    public decimal MonthlyRemaining { get; set; }
    public decimal AllocationPercent { get; set; }
    public decimal PlannedAllocation { get; set; }
}

public sealed class SavingsFundingRecommendationViewModel
{
    public string GoalName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime TargetDate { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal RequiredMonthlyContribution { get; set; }
    public decimal PlannedMonthlyAllocation { get; set; }
    public decimal AdditionalMonthlyAmountNeeded { get; set; }
}
