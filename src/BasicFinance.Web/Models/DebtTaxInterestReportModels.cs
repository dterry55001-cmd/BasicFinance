using System.ComponentModel.DataAnnotations;

namespace BasicFinance.Web.Models;

public sealed class DebtTaxInterestReportFilter
{
    [Range(2000, 2100)]
    public int Year { get; set; } = DateTime.Today.Year;
    [Required, StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$")]
    public string CurrencyCode { get; set; } = "USD";
}

public sealed class DebtTaxInterestReport
{
    public int Year { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal InterestCharged { get; set; }
    public decimal InterestEarned { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal DebtInterestPaid { get; set; }
    public decimal FeesPaid { get; set; }
    public decimal TotalDebtPayments { get; set; }
    public decimal NetInterestCost => InterestCharged - InterestEarned;
    public IReadOnlyList<DebtTaxInterestAccountRow> Accounts { get; set; } = [];
    public IReadOnlyList<DebtTaxInterestMonthRow> Monthly { get; set; } = [];
}

public sealed class DebtTaxInterestAccountRow
{
    public string AccountName { get; set; } = string.Empty;
    public decimal InterestCharged { get; set; }
    public decimal InterestEarned { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal DebtInterestPaid { get; set; }
    public decimal FeesPaid { get; set; }
    public decimal TotalPayments { get; set; }
}

public sealed class DebtTaxInterestMonthRow
{
    public int Month { get; set; }
    public decimal InterestCharged { get; set; }
    public decimal InterestEarned { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal DebtInterestPaid { get; set; }
    public decimal FeesPaid { get; set; }
    public decimal TotalPayments { get; set; }
}
