using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public enum SavingsGoalStatus
{
    Active,
    Paused,
    Completed,
    Archived
}

public enum SavingsContributionType
{
    Contribution,
    Withdrawal
}

public sealed class SavingsGoal
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal TargetAmount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string CurrencyCode { get; set; } = "USD";

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime? TargetDate { get; set; }

    public SavingsGoalStatus Status { get; set; } = SavingsGoalStatus.Active;
    public bool IsEmergencyFund { get; set; }
    public int Priority { get; set; } = 3;

    public ICollection<SavingsContribution> Contributions { get; set; } = new List<SavingsContribution>();
    public ICollection<SavingsMonthlyTarget> MonthlyTargets { get; set; } = new List<SavingsMonthlyTarget>();
    public ICollection<SavingsGoalMilestone> Milestones { get; set; } = new List<SavingsGoalMilestone>();
    public ICollection<RecurringSavingsContribution> RecurringContributions { get; set; } = new List<RecurringSavingsContribution>();
    public ICollection<SavingsAllocationSetting> AllocationSettings { get; set; } = new List<SavingsAllocationSetting>();

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SavingsContribution
{
    public int Id { get; set; }

    public int SavingsGoalId { get; set; }
    public SavingsGoal SavingsGoal { get; set; } = null!;

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    public SavingsContributionType Type { get; set; } = SavingsContributionType.Contribution;

    [DataType(DataType.Date)]
    public DateTime ContributionDate { get; set; } = DateTime.Today;

    public int? TransactionId { get; set; }
    public FinancialTransaction? Transaction { get; set; }

    public int? RecurringSavingsContributionId { get; set; }
    public DateTime? ScheduledDueDate { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EmergencyFundPlan
{
    public int Id { get; set; } = 1;

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal MonthlyEssentialExpenses { get; set; }

    [Range(1, 24)]
    public int TargetCoverageMonths { get; set; } = 6;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SavingsMonthlyTarget
{
    public int Id { get; set; }

    public int SavingsGoalId { get; set; }
    public SavingsGoal SavingsGoal { get; set; } = null!;

    [Range(2000, 2200)]
    public int Year { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal TargetAmount { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SavingsGoalMilestone
{
    public int Id { get; set; }

    public int SavingsGoalId { get; set; }
    public SavingsGoal SavingsGoal { get; set; } = null!;

    public int MilestonePercent { get; set; }
    public DateTime AchievedUtc { get; set; } = DateTime.UtcNow;
}

public enum SavingsScheduleFrequency
{
    Weekly,
    Biweekly,
    Monthly,
    Quarterly,
    Yearly
}

public sealed class RecurringSavingsContribution
{
    public int Id { get; set; }

    public int SavingsGoalId { get; set; }
    public SavingsGoal SavingsGoal { get; set; } = null!;

    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Amount { get; set; }

    public SavingsScheduleFrequency Frequency { get; set; } = SavingsScheduleFrequency.Monthly;

    [DataType(DataType.Date)]
    public DateTime NextDueDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SavingsAllocationBudget
{
    public int Id { get; set; } = 1;

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal MonthlyBudget { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SavingsAllocationSetting
{
    public int Id { get; set; }

    public int SavingsGoalId { get; set; }
    public SavingsGoal SavingsGoal { get; set; } = null!;

    [Range(typeof(decimal), "0", "100")]
    public decimal AllocationPercent { get; set; }

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
