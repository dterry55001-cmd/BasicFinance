using BasicFinance.Web.Models;

namespace BasicFinance.Web.Services;

public sealed class DebtManagementService
{

    public AccountDebtPayoffResult CalculateAccountPayoff(decimal startingBalance, decimal annualRatePercent,
        decimal minimumPayment, decimal extraMonthlyPayment, DateTime? firstPaymentDate = null)
    {
        if (startingBalance <= 0 || annualRatePercent is < 0 or > 100 || minimumPayment < 0 || extraMonthlyPayment < 0)
            throw new ArgumentOutOfRangeException(nameof(startingBalance), "Enter a positive balance and non-negative payments with an APR from 0 to 100.");
        var balance = decimal.Round(startingBalance, 2, MidpointRounding.AwayFromZero);
        var monthlyBudget = minimumPayment + extraMonthlyPayment;
        if (monthlyBudget <= 0)
            throw new InvalidOperationException("Set a minimum payment on the account or enter an additional monthly payment.");
        var monthlyRate = annualRatePercent / 1200m;
        var rows = new List<AccountDebtPayoffRow>();
        var totalInterest = 0m;
        var totalPaid = 0m;
        var start = (firstPaymentDate ?? DateTime.Today.AddMonths(1)).Date;
        for (var month = 1; month <= 1200 && balance > 0; month++)
        {
            var opening = balance;
            var interest = decimal.Round(balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
            var payment = Math.Min(balance + interest, monthlyBudget);
            if (payment <= interest)
                throw new InvalidOperationException("The monthly payment does not cover interest. Increase the payment amount.");
            var principal = payment - interest;
            balance = Math.Max(0m, decimal.Round(balance - principal, 2, MidpointRounding.AwayFromZero));
            totalInterest += interest;
            totalPaid += payment;
            rows.Add(new AccountDebtPayoffRow { MonthNumber = month, PaymentDate = start.AddMonths(month - 1),
                StartingBalance = opening, Interest = interest, Payment = payment, Principal = principal, RemainingBalance = balance });
        }
        if (balance > 0) throw new InvalidOperationException("This payment plan exceeds the 100-year planning limit. Increase the monthly payment.");
        return new AccountDebtPayoffResult { StartingBalance = startingBalance, MonthlyMinimum = minimumPayment,
            ExtraMonthlyPayment = extraMonthlyPayment, TotalInterest = totalInterest, TotalPaid = totalPaid,
            PayoffDate = rows[^1].PaymentDate, Payments = rows };
    }

    public LoanAmortizationResult CalculateAmortization(LoanAmortizationInput input)
    {
        if (input.Principal <= 0 || input.TermMonths is < 1 or > 600 || input.AnnualRatePercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(input), "Enter a positive principal, a valid APR, and a term from 1 to 600 months.");

        var monthlyRate = input.AnnualRatePercent / 100m / 12m;
        var regularPayment = monthlyRate == 0
            ? input.Principal / input.TermMonths
            : input.Principal * monthlyRate / (1m - (decimal)Math.Pow((double)(1m + monthlyRate), -input.TermMonths));

        regularPayment = decimal.Round(regularPayment, 2, MidpointRounding.AwayFromZero);
        var balance = input.Principal;
        var totalInterest = 0m;
        var rows = new List<LoanPaymentRow>(input.TermMonths);

        for (var month = 1; month <= input.TermMonths && balance > 0; month++)
        {
            var interest = decimal.Round(balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
            var payment = Math.Min(regularPayment, balance + interest);
            var principal = payment - interest;
            if (principal <= 0)
                throw new InvalidOperationException("The scheduled payment does not reduce principal. Increase the payment or review the APR.");

            balance = Math.Max(0, balance - principal);
            totalInterest += interest;
            rows.Add(new LoanPaymentRow
            {
                PaymentNumber = month,
                PaymentDate = input.FirstPaymentDate.Date.AddMonths(month - 1),
                Payment = payment,
                Principal = principal,
                Interest = interest,
                RemainingBalance = balance
            });
        }

        return new LoanAmortizationResult
        {
            RegularPayment = regularPayment,
            TotalInterest = totalInterest,
            TotalPaid = rows.Sum(row => row.Payment),
            Payments = rows
        };
    }

    public DebtPayoffPlan CalculatePayoff(
        IReadOnlyCollection<DebtPayoffDebtInput> debts,
        decimal extraMonthlyPayment,
        bool avalanche,
        DateTime? startDate = null)
    {
        if (extraMonthlyPayment < 0) throw new ArgumentOutOfRangeException(nameof(extraMonthlyPayment));
        if (debts.Any(debt => debt.Balance < 0 || debt.AnnualRatePercent is < 0 or > 100 || debt.MinimumPayment < 0))
            throw new ArgumentOutOfRangeException(nameof(debts), "Debt balances, APRs, and minimum payments must be non-negative and APR must not exceed 100.");

        var balances = debts.Where(debt => debt.Balance > 0)
            .ToDictionary(debt => debt.AccountId, debt => decimal.Round(debt.Balance, 2));
        var interestByDebt = debts.ToDictionary(debt => debt.AccountId, _ => 0m);
        var payoffMonthByDebt = new Dictionary<int, int>();
        var totalInterest = 0m;
        var totalPaid = 0m;
        var month = 0;
        var planStart = (startDate ?? DateTime.Today).Date;

        while (balances.Values.Any(value => value > 0) && month < 1200)
        {
            month++;
            var monthlyInterest = new Dictionary<int, decimal>();
            foreach (var debt in debts.Where(debt => balances.GetValueOrDefault(debt.AccountId) > 0))
            {
                var interest = decimal.Round(balances[debt.AccountId] * debt.AnnualRatePercent / 1200m, 2, MidpointRounding.AwayFromZero);
                balances[debt.AccountId] += interest;
                monthlyInterest[debt.AccountId] = interest;
                interestByDebt[debt.AccountId] += interest;
                totalInterest += interest;
            }

            var remainingBudget = debts.Sum(debt =>
            {
                if (!balances.TryGetValue(debt.AccountId, out var balance) || balance <= 0) return 0m;
                var fallback = Math.Max(1m, decimal.Round(balance * 0.01m, 2));
                return Math.Min(balance, Math.Max(debt.MinimumPayment, fallback));
            }) + extraMonthlyPayment;

            foreach (var debt in debts)
            {
                if (!balances.TryGetValue(debt.AccountId, out var balance) || balance <= 0) continue;
                var minimum = Math.Min(balance, Math.Max(debt.MinimumPayment, Math.Max(1m, decimal.Round(balance * 0.01m, 2))));
                var payment = Math.Min(balance, Math.Min(minimum, remainingBudget));
                balances[debt.AccountId] -= payment;
                remainingBudget -= payment;
                totalPaid += payment;
                if (balances[debt.AccountId] == 0) payoffMonthByDebt.TryAdd(debt.AccountId, month);
            }

            var priority = debts.Where(debt => balances.GetValueOrDefault(debt.AccountId) > 0);
            priority = avalanche
                ? priority.OrderByDescending(debt => debt.AnnualRatePercent).ThenBy(debt => balances[debt.AccountId])
                : priority.OrderBy(debt => balances[debt.AccountId]).ThenByDescending(debt => debt.AnnualRatePercent);

            foreach (var debt in priority)
            {
                if (remainingBudget <= 0) break;
                var payment = Math.Min(balances[debt.AccountId], remainingBudget);
                balances[debt.AccountId] -= payment;
                remainingBudget -= payment;
                totalPaid += payment;
                if (balances[debt.AccountId] == 0) payoffMonthByDebt.TryAdd(debt.AccountId, month);
            }

            if (remainingBudget > 0 && balances.Values.All(value => value <= 0)) break;
        }

        var reachedDebtFree = balances.Values.All(value => value <= 0);
        var payoffLines = debts.Select(debt =>
        {
            var payoffMonth = payoffMonthByDebt.GetValueOrDefault(debt.AccountId, reachedDebtFree ? month : 1200);
            return new DebtPayoffLine
            {
                AccountId = debt.AccountId,
                AccountName = debt.Name,
                MonthsToPayoff = payoffMonth,
                PayoffDate = planStart.AddMonths(payoffMonth),
                InterestPaid = interestByDebt.GetValueOrDefault(debt.AccountId)
            };
        }).ToList();

        return new DebtPayoffPlan
        {
            Strategy = avalanche ? "Avalanche" : "Snowball",
            MonthsToPayoff = reachedDebtFree ? month : 1200,
            DebtFreeDate = planStart.AddMonths(reachedDebtFree ? month : 1200),
            TotalInterest = totalInterest,
            TotalPaid = totalPaid,
            ReachedDebtFree = reachedDebtFree,
            Debts = payoffLines
        };
    }
    public IReadOnlyList<EarlyPayoffScenarioResult> CalculateEarlyPayoffScenarios(
        decimal startingBalance, decimal annualRatePercent, decimal minimumPayment,
        decimal oneTimePayment, int oneTimePaymentMonth, decimal extraMonthlyPayment,
        DateTime? firstPaymentDate = null)
    {
        if (oneTimePayment < 0 || extraMonthlyPayment < 0 || oneTimePaymentMonth < 1)
            throw new ArgumentOutOfRangeException(nameof(oneTimePayment), "Additional payments and the one-time payment month must be non-negative and valid.");
        var baseline = CalculateEarlyPayoff(startingBalance, annualRatePercent, minimumPayment, 0m, 1, 0m, firstPaymentDate);
        var lump = CalculateEarlyPayoff(startingBalance, annualRatePercent, minimumPayment, oneTimePayment, oneTimePaymentMonth, 0m, firstPaymentDate);
        var monthly = CalculateEarlyPayoff(startingBalance, annualRatePercent, minimumPayment, 0m, 1, extraMonthlyPayment, firstPaymentDate);
        var combined = CalculateEarlyPayoff(startingBalance, annualRatePercent, minimumPayment, oneTimePayment, oneTimePaymentMonth, extraMonthlyPayment, firstPaymentDate);
        var scenarios = new[] { baseline, lump, monthly, combined };
        foreach (var scenario in scenarios)
        {
            scenario.InterestSavings = baseline.TotalInterest - scenario.TotalInterest;
            scenario.MonthsSaved = baseline.MonthsToPayoff - scenario.MonthsToPayoff;
        }
        return scenarios;
    }

    private static EarlyPayoffScenarioResult CalculateEarlyPayoff(
        decimal startingBalance, decimal annualRatePercent, decimal minimumPayment,
        decimal oneTimePayment, int oneTimePaymentMonth, decimal extraMonthlyPayment,
        DateTime? firstPaymentDate)
    {
        if (startingBalance <= 0 || annualRatePercent is < 0 or > 100 || minimumPayment < 0)
            throw new ArgumentOutOfRangeException(nameof(startingBalance), "Enter a positive balance, valid APR, and non-negative minimum payment.");
        if (minimumPayment + extraMonthlyPayment <= 0)
            throw new InvalidOperationException("The monthly payment must be greater than zero.");
        var balance = decimal.Round(startingBalance, 2, MidpointRounding.AwayFromZero);
        var monthlyRate = annualRatePercent / 1200m;
        var rows = new List<AccountDebtPayoffRow>();
        var totalInterest = 0m;
        var totalPaid = 0m;
        var firstDate = (firstPaymentDate ?? DateTime.Today.AddMonths(1)).Date;
        for (var month = 1; month <= 1200 && balance > 0; month++)
        {
            var opening = balance;
            var interest = decimal.Round(balance * monthlyRate, 2, MidpointRounding.AwayFromZero);
            var due = balance + interest;
            var scheduled = Math.Min(due, minimumPayment + extraMonthlyPayment);
            if (scheduled <= interest)
                throw new InvalidOperationException("The monthly payment does not cover interest. Increase the payment amount.");
            var principal = scheduled - interest;
            balance = Math.Max(0m, decimal.Round(balance - principal, 2, MidpointRounding.AwayFromZero));
            var payment = scheduled;
            if (month == oneTimePaymentMonth && balance > 0 && oneTimePayment > 0)
            {
                var lump = Math.Min(balance, oneTimePayment);
                balance = decimal.Round(balance - lump, 2, MidpointRounding.AwayFromZero);
                principal += lump;
                payment += lump;
            }
            totalInterest += interest;
            totalPaid += payment;
            rows.Add(new AccountDebtPayoffRow { MonthNumber = month, PaymentDate = firstDate.AddMonths(month - 1),
                StartingBalance = opening, Interest = interest, Payment = payment, Principal = principal, RemainingBalance = balance });
        }
        if (balance > 0) throw new InvalidOperationException("This payment plan exceeds the 100-year planning limit.");
        return new EarlyPayoffScenarioResult
        {
            Strategy = oneTimePayment > 0 && extraMonthlyPayment > 0 ? "One-time + monthly extra"
                : oneTimePayment > 0 ? "One-time extra payment"
                : extraMonthlyPayment > 0 ? "Monthly extra payment" : "Current payment",
            TotalInterest = totalInterest, TotalPaid = totalPaid, MonthsToPayoff = rows.Count,
            PayoffDate = rows[^1].PaymentDate, Payments = rows
        };
    }

}
