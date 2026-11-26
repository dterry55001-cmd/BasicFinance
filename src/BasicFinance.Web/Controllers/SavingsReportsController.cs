using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace BasicFinance.Web.Controllers;

public sealed class SavingsReportsController(
    ApplicationDbContext database,
    SavingsGoalService savingsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var selectedYear = Math.Clamp(year ?? today.Year, 2000, 2200);
        var selectedMonth = month.HasValue ? Math.Clamp(month.Value, 1, 12) : today.Month;
        var model = await BuildReportAsync(selectedYear, selectedMonth, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Export(int? year, int? month, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var selectedYear = Math.Clamp(year ?? today.Year, 2000, 2200);
        var selectedMonth = month.HasValue ? Math.Clamp(month.Value, 1, 12) : today.Month;
        var model = await BuildReportAsync(selectedYear, selectedMonth, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Period,Goal,Planned Target,Actual Contributions,Withdrawals,Net Savings,Variance,Closing Balance,Estimated Completion");
        foreach (var row in model.GoalRows)
        {
            csv.AppendLine(string.Join(",",
                Csv(row.PeriodLabel), Csv(row.GoalName),
                row.PlannedTarget.ToString("0.00", CultureInfo.InvariantCulture),
                row.Contributions.ToString("0.00", CultureInfo.InvariantCulture),
                row.Withdrawals.ToString("0.00", CultureInfo.InvariantCulture),
                row.NetSavings.ToString("0.00", CultureInfo.InvariantCulture),
                row.Variance.ToString("0.00", CultureInfo.InvariantCulture),
                row.ClosingBalance.ToString("0.00", CultureInfo.InvariantCulture),
                Csv(row.EstimatedCompletionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "")));
        }

        var bytes = new UTF8Encoding(true).GetBytes(csv.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"savings-report-{selectedYear}-{selectedMonth:00}.csv");
    }

    private async Task<SavingsReportsViewModel> BuildReportAsync(int year, int month, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var isCurrentMonth = start.Year == today.Year && start.Month == today.Month;
        var reportEnd = isCurrentMonth ? today : end.AddDays(-1);

        var goals = await database.SavingsGoals.AsNoTracking()
            .Include(x => x.Contributions)
            .Include(x => x.MonthlyTargets)
            .Where(x => x.CurrencyCode == "USD" && x.Status != SavingsGoalStatus.Archived)
            .OrderBy(x => x.Priority).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var goalRows = goals.Select(goal =>
        {
            var entries = goal.Contributions
                .Where(x => x.ContributionDate.Date >= start && x.ContributionDate.Date < end)
                .ToList();
            var contributions = entries.Where(x => x.Type == SavingsContributionType.Contribution).Sum(x => x.Amount);
            var withdrawals = entries.Where(x => x.Type == SavingsContributionType.Withdrawal).Sum(x => x.Amount);
            var planned = goal.MonthlyTargets.FirstOrDefault(x => x.Year == year && x.Month == month)?.TargetAmount ?? 0m;
            var balance = goal.Contributions.Where(x => x.ContributionDate.Date <= reportEnd)
                .Sum(x => x.Type == SavingsContributionType.Contribution ? x.Amount : -x.Amount);
            var progress = savingsService.CalculateProgress(goal, reportEnd);
            return new SavingsReportGoalRow
            {
                GoalId = goal.Id,
                GoalName = goal.Name,
                IsEmergencyFund = goal.IsEmergencyFund,
                PeriodLabel = start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                PlannedTarget = planned,
                Contributions = contributions,
                Withdrawals = withdrawals,
                NetSavings = contributions - withdrawals,
                Variance = contributions - planned,
                ClosingBalance = balance,
                EstimatedCompletionDate = goal.Status == SavingsGoalStatus.Active
                    ? progress.EstimatedCompletionDate : null,
                TargetAmount = goal.TargetAmount,
                ProgressPercent = progress.ProgressPercent
            };
        }).ToList();

        var emergencyGoal = goals.FirstOrDefault(x => x.IsEmergencyFund);
        var emergencyRows = new List<SavingsEmergencyTrendPoint>();
        if (emergencyGoal is not null)
        {
            for (var offset = 11; offset >= 0; offset--)
            {
                var trendMonth = new DateTime(year, month, 1).AddMonths(-offset);
                var trendEnd = trendMonth.AddMonths(1).AddDays(-1);
                if (trendEnd > today) trendEnd = today;
                var balance = emergencyGoal.Contributions
                    .Where(x => x.ContributionDate.Date <= trendEnd)
                    .Sum(x => x.Type == SavingsContributionType.Contribution ? x.Amount : -x.Amount);
                emergencyRows.Add(new SavingsEmergencyTrendPoint
                {
                    Month = trendMonth,
                    ClosingBalance = balance
                });
            }
        }

        var annualMonths = Enumerable.Range(1, 12).Select(monthNumber =>
        {
            var monthStart = new DateTime(year, monthNumber, 1);
            var monthEnd = monthStart.AddMonths(1);
            var cutoff = monthEnd.AddDays(-1);
            if (monthStart.Year == today.Year && monthStart.Month == today.Month) cutoff = today;
            if (monthStart > today) cutoff = monthStart.AddDays(-1);
            var actual = goals.SelectMany(x => x.Contributions)
                .Where(x => x.ContributionDate.Date >= monthStart && x.ContributionDate.Date < monthEnd
                    && x.ContributionDate.Date <= today)
                .ToList();
            return new SavingsReportMonthRow
            {
                Month = monthStart,
                Contributions = actual.Where(x => x.Type == SavingsContributionType.Contribution).Sum(x => x.Amount),
                Withdrawals = actual.Where(x => x.Type == SavingsContributionType.Withdrawal).Sum(x => x.Amount),
                PlannedTarget = goals.Sum(x => x.MonthlyTargets.FirstOrDefault(t => t.Year == year && t.Month == monthNumber)?.TargetAmount ?? 0m)
            };
        }).ToList();

        var selectedRows = goalRows.Where(x => x.Contributions != 0m || x.Withdrawals != 0m || x.PlannedTarget != 0m
            || x.ClosingBalance != 0m).ToList();

        return new SavingsReportsViewModel
        {
            Year = year,
            Month = month,
            PeriodLabel = start.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            GoalRows = selectedRows,
            AnnualMonths = annualMonths,
            EmergencyTrend = emergencyRows,
            TotalPlannedTarget = goalRows.Sum(x => x.PlannedTarget),
            TotalContributions = goalRows.Sum(x => x.Contributions),
            TotalWithdrawals = goalRows.Sum(x => x.Withdrawals),
            TotalNetSavings = goalRows.Sum(x => x.NetSavings),
            TotalVariance = goalRows.Sum(x => x.Variance),
            IsCurrentMonth = isCurrentMonth
        };
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}

public sealed class SavingsReportsViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public bool IsCurrentMonth { get; set; }
    public List<SavingsReportGoalRow> GoalRows { get; set; } = [];
    public List<SavingsReportMonthRow> AnnualMonths { get; set; } = [];
    public List<SavingsEmergencyTrendPoint> EmergencyTrend { get; set; } = [];
    public decimal TotalPlannedTarget { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal TotalNetSavings { get; set; }
    public decimal TotalVariance { get; set; }
}

public sealed class SavingsReportGoalRow
{
    public int GoalId { get; set; }
    public string GoalName { get; set; } = string.Empty;
    public bool IsEmergencyFund { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal PlannedTarget { get; set; }
    public decimal Contributions { get; set; }
    public decimal Withdrawals { get; set; }
    public decimal NetSavings { get; set; }
    public decimal Variance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal ProgressPercent { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
}

public sealed class SavingsReportMonthRow
{
    public DateTime Month { get; set; }
    public decimal Contributions { get; set; }
    public decimal Withdrawals { get; set; }
    public decimal PlannedTarget { get; set; }
    public decimal NetSavings => Contributions - Withdrawals;
    public decimal Variance => Contributions - PlannedTarget;
}

public sealed class SavingsEmergencyTrendPoint
{
    public DateTime Month { get; set; }
    public decimal ClosingBalance { get; set; }
}
