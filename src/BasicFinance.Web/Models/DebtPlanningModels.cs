using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtToIncomeRecord
{
    public int Id { get; set; }
    [DataType(DataType.Date)]
    public DateTime Month { get; set; }
    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$")]
    public string CurrencyCode { get; set; } = "USD";
    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal GrossMonthlyIncome { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal HousingMonthlyPayment { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    public decimal OtherMonthlyDebtPayment { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Net monthly income")]
    public decimal NetMonthlyIncome { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Other essential monthly expenses")]
    public decimal OtherEssentialMonthlyExpenses { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public decimal HousingRatioPercent => GrossMonthlyIncome <= 0 ? 0 : HousingMonthlyPayment / GrossMonthlyIncome * 100m;
    public decimal TotalDebtRatioPercent => GrossMonthlyIncome <= 0 ? 0 : (HousingMonthlyPayment + OtherMonthlyDebtPayment) / GrossMonthlyIncome * 100m;
    public decimal TotalMonthlyDebtObligations => HousingMonthlyPayment + OtherMonthlyDebtPayment;
    public decimal MonthlyIncomeAfterObligations => NetMonthlyIncome - TotalMonthlyDebtObligations - OtherEssentialMonthlyExpenses;
    public decimal DebtPaymentToNetIncomePercent => NetMonthlyIncome <= 0 ? 0 : TotalMonthlyDebtObligations / NetMonthlyIncome * 100m;
    public decimal AvailableForAdditionalDebtPayment => Math.Max(0m, MonthlyIncomeAfterObligations);
    public string AffordabilityStatus => NetMonthlyIncome <= 0 ? "Net income not recorded" :
        MonthlyIncomeAfterObligations < 0 ? "Expenses exceed net income" :
        MonthlyIncomeAfterObligations < NetMonthlyIncome * 0.10m ? "Limited flexibility" : "Positive cash flow";
}

public sealed class EarlyPayoffScenarioInput
{
    public int AccountId { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "One-time additional payment")]
    public decimal OneTimePayment { get; set; }
    [Range(1, 1200)]
    [Display(Name = "Apply one-time payment in month")]
    public int OneTimePaymentMonth { get; set; } = 1;
    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Additional monthly payment")]
    public decimal ExtraMonthlyPayment { get; set; }
}

public sealed class EarlyPayoffScenarioResult
{
    public string Strategy { get; set; } = string.Empty;
    public decimal TotalInterest { get; set; }
    public decimal TotalPaid { get; set; }
    public DateTime PayoffDate { get; set; }
    public int MonthsToPayoff { get; set; }
    public decimal InterestSavings { get; set; }
    public int MonthsSaved { get; set; }
    public IReadOnlyList<AccountDebtPayoffRow> Payments { get; set; } = [];
}
