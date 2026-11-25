using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtScenarioInput
{
    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Additional monthly debt budget")]
    public decimal ExtraMonthlyBudget { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "One-time lump-sum payment")]
    public decimal LumpSumPayment { get; set; }

    [Range(1, 1200)]
    [Display(Name = "Lump-sum payment month")]
    public int LumpSumMonth { get; set; } = 1;

    [Range(typeof(decimal), "0", "100")]
    [Display(Name = "APR adjustment (percentage points)")]
    public decimal AprAdjustment { get; set; }

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Monthly payment budget override")]
    public decimal? MonthlyBudgetOverride { get; set; }

    [DataType(DataType.Date)]
    public DateTime FirstPaymentDate { get; set; } = DateTime.Today.AddMonths(1);
}

public sealed class DebtScenarioDebt
{
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public decimal StartingBalance { get; set; }
    public decimal AnnualRatePercent { get; set; }
    public decimal MinimumPayment { get; set; }
}

public sealed class DebtScenarioPaymentRow
{
    public int Month { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Payment { get; set; }
    public decimal Interest { get; set; }
    public decimal RemainingBalance { get; set; }
}

public sealed class DebtScenarioDebtResult
{
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MonthsToPayoff { get; set; }
    public DateTime PayoffDate { get; set; }
    public decimal InterestPaid { get; set; }
}

public sealed class DebtScenarioResult
{
    public string Strategy { get; set; } = string.Empty;
    public decimal StartingBalance { get; set; }
    public decimal MonthlyBudget { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal TotalPaid { get; set; }
    public int MonthsToPayoff { get; set; }
    public DateTime DebtFreeDate { get; set; }
    public bool ReachedDebtFree { get; set; }
    public IReadOnlyList<DebtScenarioDebtResult> Debts { get; set; } = [];
    public IReadOnlyList<DebtScenarioPaymentRow> Payments { get; set; } = [];
}

public sealed class DebtScenarioComparison
{
    public DebtScenarioResult Baseline { get; set; } = new();
    public DebtScenarioResult Snowball { get; set; } = new();
    public DebtScenarioResult Avalanche { get; set; } = new();
    public decimal InterestSavingsVsBaseline { get; set; }
    public int MonthsSavedVsBaseline { get; set; }
}
