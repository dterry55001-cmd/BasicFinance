using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtPayoffInputModel
{
    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$")]
    [Display(Name = "Currency")]
    public string CurrencyCode { get; set; } = "USD";

    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Additional monthly payment")]
    public decimal ExtraMonthlyPayment { get; set; }
}

public sealed class DebtPayoffDebtInput
{
    public int AccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal AnnualRatePercent { get; set; }
    public decimal MinimumPayment { get; set; }
}

public sealed class DebtPayoffLine
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int MonthsToPayoff { get; set; }
    public DateTime PayoffDate { get; set; }
    public decimal InterestPaid { get; set; }
}

public sealed class DebtPayoffPlan
{
    public string Strategy { get; set; } = string.Empty;
    public int MonthsToPayoff { get; set; }
    public DateTime DebtFreeDate { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal TotalPaid { get; set; }
    public bool ReachedDebtFree { get; set; }
    public IReadOnlyList<DebtPayoffLine> Debts { get; set; } = [];
}

public sealed class LoanAmortizationInput
{
    public int AccountId { get; set; }
    [Range(typeof(decimal), "0.01", "999999999999")]
    public decimal Principal { get; set; }
    [Range(typeof(decimal), "0", "100")]
    public decimal AnnualRatePercent { get; set; }
    [Range(1, 600)]
    public int TermMonths { get; set; }
    [DataType(DataType.Date)]
    public DateTime FirstPaymentDate { get; set; } = DateTime.Today.AddMonths(1);
}

public sealed class LoanPaymentRow
{
    public int PaymentNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Payment { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal RemainingBalance { get; set; }
}

public sealed class LoanAmortizationResult
{
    public decimal RegularPayment { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal TotalPaid { get; set; }
    public IReadOnlyList<LoanPaymentRow> Payments { get; set; } = [];
}


public sealed class AccountDebtPayoffInput
{
    public int AccountId { get; set; }
    [Range(typeof(decimal), "0", "999999999999")]
    [Display(Name = "Additional monthly payment")]
    public decimal ExtraMonthlyPayment { get; set; }
}

public sealed class AccountDebtPayoffRow
{
    public int MonthNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal StartingBalance { get; set; }
    public decimal Interest { get; set; }
    public decimal Payment { get; set; }
    public decimal Principal { get; set; }
    public decimal RemainingBalance { get; set; }
}

public sealed class AccountDebtPayoffResult
{
    public decimal StartingBalance { get; set; }
    public decimal MonthlyMinimum { get; set; }
    public decimal ExtraMonthlyPayment { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal TotalPaid { get; set; }
    public DateTime PayoffDate { get; set; }
    public IReadOnlyList<AccountDebtPayoffRow> Payments { get; set; } = [];
}
