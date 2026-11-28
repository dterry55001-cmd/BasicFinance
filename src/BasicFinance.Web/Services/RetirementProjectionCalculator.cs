using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

/// <summary>
/// Deterministic retirement projection calculator. It estimates future values only;
/// it never changes account balances or creates financial transactions.
/// </summary>
public sealed class RetirementProjectionCalculator
{
    public RetirementProjectionResult Calculate(RetirementProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var years = input.RetirementAge - input.CurrentAge;
        var balance = input.CurrentBalance;
        var employeeTotal = 0m;
        var employerTotal = 0m;
        var growthTotal = 0m;
        var annualRate = (decimal)Math.Pow(1d + (double)(input.AnnualReturnPercent / 100m), 1d / 12d) - 1m;
        var monthlyFeeRate = input.AnnualFeePercent / 100m / 12m;
        var monthlyInflationRate = (decimal)Math.Pow(
            1d + (double)(input.AnnualInflationPercent / 100m), 1d / 12d) - 1m;
        var employeeContribution = input.MonthlyEmployeeContribution;
        var rows = new List<RetirementProjectionYear>(years);

        for (var year = 1; year <= years; year++)
        {
            var yearEmployee = 0m;
            var yearEmployer = 0m;
            var openingBalance = balance;

            for (var month = 0; month < 12; month++)
            {
                var investmentGrowth = balance * (annualRate - monthlyFeeRate);
                balance += investmentGrowth;
                growthTotal += investmentGrowth;

                var employee = employeeContribution;
                var employer = employee * input.EmployerMatchPercent / 100m;
                if (input.EmployerMatchCapMonthly.HasValue)
                    employer = Math.Min(employer, input.EmployerMatchCapMonthly.Value);

                balance += employee + employer;
                employeeTotal += employee;
                employerTotal += employer;
                yearEmployee += employee;
                yearEmployer += employer;
            }

            var elapsedMonths = year * 12;
            var inflationFactor = (decimal)Math.Pow(
                1d + (double)(input.AnnualInflationPercent / 100m), elapsedMonths / 12d);
            rows.Add(new RetirementProjectionYear(
                input.CurrentAge + year,
                elapsedMonths,
                yearEmployee,
                yearEmployer,
                balance - openingBalance - yearEmployee - yearEmployer,
                decimal.Round(balance, 2, MidpointRounding.ToEven),
                decimal.Round(balance / inflationFactor, 2, MidpointRounding.ToEven)));

            employeeContribution *= 1m + input.AnnualContributionIncreasePercent / 100m;
        }

        var totalInflationFactor = (decimal)Math.Pow(
            1d + (double)(input.AnnualInflationPercent / 100m), years);
        return new RetirementProjectionResult(
            input.CurrencyCode,
            years,
            input.CurrentBalance,
            decimal.Round(employeeTotal, 2, MidpointRounding.ToEven),
            decimal.Round(employerTotal, 2, MidpointRounding.ToEven),
            decimal.Round(growthTotal, 2, MidpointRounding.ToEven),
            decimal.Round(balance, 2, MidpointRounding.ToEven),
            decimal.Round(balance / totalInflationFactor, 2, MidpointRounding.ToEven),
            rows);
    }

    private static void Validate(RetirementProjectionInput input)
    {
        if (input.CurrentAge is < 18 or > 100)
            throw new ArgumentOutOfRangeException(nameof(input.CurrentAge), "Current age must be between 18 and 100.");
        if (input.RetirementAge <= input.CurrentAge || input.RetirementAge > 100)
            throw new ArgumentOutOfRangeException(nameof(input.RetirementAge), "Retirement age must be greater than current age and no more than 100.");
        if (input.CurrentBalance < 0m)
            throw new ArgumentOutOfRangeException(nameof(input.CurrentBalance));
        if (input.MonthlyEmployeeContribution < 0m)
            throw new ArgumentOutOfRangeException(nameof(input.MonthlyEmployeeContribution));
        if (input.EmployerMatchPercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(input.EmployerMatchPercent));
        if (input.EmployerMatchCapMonthly < 0m)
            throw new ArgumentOutOfRangeException(nameof(input.EmployerMatchCapMonthly));
        if (input.AnnualReturnPercent is < -100m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(input.AnnualReturnPercent));
        if (input.AnnualFeePercent is < 0m or > 20m)
            throw new ArgumentOutOfRangeException(nameof(input.AnnualFeePercent));
        if (input.AnnualInflationPercent is < 0m or > 50m)
            throw new ArgumentOutOfRangeException(nameof(input.AnnualInflationPercent));
        if (input.AnnualContributionIncreasePercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(input.AnnualContributionIncreasePercent));
        if (string.IsNullOrWhiteSpace(input.CurrencyCode) ||
            input.CurrencyCode.Length != 3 ||
            input.CurrencyCode.Any(character => !char.IsAsciiLetterUpper(character)))
            throw new ArgumentException("Currency code must be three uppercase letters.", nameof(input.CurrencyCode));
    }
}
