using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtFreeTarget
{
    public int Id { get; set; }
    [Required, StringLength(3, MinimumLength = 3), RegularExpression(@"^[A-Z]{3}$")]
    public string CurrencyCode { get; set; } = "USD";
    [DataType(DataType.Date)]
    public DateTime TargetDate { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal ExtraMonthlyPayment { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MonthlyDebtReductionGoal
{
    public int Id { get; set; }
    [Required, StringLength(3, MinimumLength = 3), RegularExpression(@"^[A-Z]{3}$")]
    public string CurrencyCode { get; set; } = "USD";
    [DataType(DataType.Date)]
    public DateTime Month { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal TargetReduction { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DebtFreeMilestone
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;
    [DataType(DataType.Date)]
    public DateTime AchievedDate { get; set; }
    public bool IsReopened { get; set; }
    [DataType(DataType.Date)]
    public DateTime? ReopenedDate { get; set; }
    [StringLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MonthlyDebtGoalProgress
{
    public MonthlyDebtReductionGoal Goal { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal ActualReduction { get; set; }
    public decimal RemainingToGoal { get; set; }
}

public sealed class DebtGoalsPageViewModel
{
    public List<DebtFreeTarget> Targets { get; set; } = [];
    public List<MonthlyDebtGoalProgress> MonthlyGoals { get; set; } = [];
    public List<DebtFreeMilestone> Milestones { get; set; } = [];
    public List<DebtPayoffEstimateRow> Estimates { get; set; } = [];
    public List<DebtCurrencyBalance> Balances { get; set; } = [];
}

public sealed record DebtCurrencyBalance(string CurrencyCode, decimal Balance, int AccountCount);

public sealed class DebtPayoffEstimateRow
{
    public string CurrencyCode { get; set; } = "USD";
    public decimal TotalBalance { get; set; }
    public DateTime? EstimatedDebtFreeDate { get; set; }
    public DateTime? TargetDebtFreeDate { get; set; }
    public bool IsComplete { get; set; }
    public string? MissingInputs { get; set; }
    public decimal EstimatedInterest { get; set; }
    public int? MonthsRemaining { get; set; }
}
