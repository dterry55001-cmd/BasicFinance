using BasicFinance.Web.Data;
using BasicFinance.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BasicFinance.Tests;

public sealed class FinancialPlanningDataProviderTests
{
    [Fact]
    public async Task GetPlanningDataAsync_UsesHistoricalEndOfDayAndKeepsCurrenciesSeparate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var account = new BasicFinance.Web.Models.Account
        {
            Name = "Checking", Type = BasicFinance.Web.Models.AccountType.Checking,
            CurrencyCode = "USD", OpeningBalance = 100m
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        db.Transactions.AddRange(
            new BasicFinance.Web.Models.FinancialTransaction
            {
                AccountId = account.Id, Type = BasicFinance.Web.Models.TransactionType.Income,
                Description = "As-of income", Amount = 50m, CurrencyCode = "USD",
                TransactionDate = new DateTime(2026, 11, 27, 18, 0, 0)
            },
            new BasicFinance.Web.Models.FinancialTransaction
            {
                AccountId = account.Id, Type = BasicFinance.Web.Models.TransactionType.Income,
                Description = "Future income", Amount = 900m, CurrencyCode = "USD",
                TransactionDate = new DateTime(2026, 11, 28)
            });
        await db.SaveChangesAsync();

        var provider = new FinancialPlanningDataProvider(db, new AccountBalanceService(db));
        var result = await provider.GetPlanningDataAsync(new DateOnly(2026, 11, 27), "USD");

        Assert.Single(result.Accounts);
        Assert.Equal(150m, result.Accounts[0].Balance);
        Assert.Empty(result.Debts);
        Assert.Contains(result.Sources, x => x.SourceName == "Accounts" && x.Status == PlanningSourceStatus.Available);
    }

    [Fact]
    public async Task GetPlanningDataAsync_RejectsInvalidReportingCurrency()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        var provider = new FinancialPlanningDataProvider(db, new AccountBalanceService(db));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.GetPlanningDataAsync(new DateOnly(2026, 11, 27), "usd"));
    }
    [Fact]
    public async Task GetPlanningDataAsync_PreservesDebtGapsAndSavingsAsOfProgressWithoutDoubleCounting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var debt = new BasicFinance.Web.Models.Account
        {
            Name = "Unspecified loan", Type = BasicFinance.Web.Models.AccountType.Debt,
            DebtType = BasicFinance.Web.Models.DebtAccountType.PersonalLoan,
            CurrencyCode = "USD", OpeningBalance = 2500m,
            InterestRatePercent = null, MinimumPayment = null
        };
        var retirement = new BasicFinance.Web.Models.Account
        {
            Name = "Retirement", Type = BasicFinance.Web.Models.AccountType.Retirement,
            RetirementType = BasicFinance.Web.Models.RetirementAccountType.RothIra,
            CurrencyCode = "EUR", OpeningBalance = 12000m
        };
        var goal = new BasicFinance.Web.Models.SavingsGoal
        {
            Name = "Emergency reserve", TargetAmount = 6000m, CurrencyCode = "EUR",
            IsEmergencyFund = true, LinkedAccount = retirement,
            Contributions =
            [
                new BasicFinance.Web.Models.SavingsContribution
                {
                    Amount = 700m, Type = BasicFinance.Web.Models.SavingsContributionType.Contribution,
                    ContributionDate = new DateTime(2026, 11, 20)
                },
                new BasicFinance.Web.Models.SavingsContribution
                {
                    Amount = 100m, Type = BasicFinance.Web.Models.SavingsContributionType.Withdrawal,
                    ContributionDate = new DateTime(2026, 11, 21)
                },
                new BasicFinance.Web.Models.SavingsContribution
                {
                    Amount = 900m, Type = BasicFinance.Web.Models.SavingsContributionType.Contribution,
                    ContributionDate = new DateTime(2026, 11, 28)
                }
            ]
        };
        db.Accounts.AddRange(debt, retirement);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();

        var provider = new FinancialPlanningDataProvider(db, new AccountBalanceService(db));
        var result = await provider.GetPlanningDataAsync(new DateOnly(2026, 11, 27), "USD");

        var debtInput = Assert.Single(result.Debts);
        Assert.Null(debtInput.AnnualRatePercent);
        Assert.Null(debtInput.MinimumPayment);
        var savings = Assert.Single(result.SavingsGoals);
        Assert.Equal(600m, savings.CurrentAmount);
        Assert.Equal("EUR", savings.CurrencyCode);
        Assert.Contains(result.Warnings, warning => warning.Contains("interest rate and minimum payment"));
        Assert.Contains(result.Warnings, warning => warning.Contains("No FX conversion"));
        Assert.Contains(result.Warnings, warning => warning.Contains("progress metadata"));
        Assert.Contains(result.Warnings, warning => warning.Contains("Retirement balances"));
        Assert.Empty(result.FutureSavingsSchedules);
        Assert.Empty(result.FutureCashFlowSchedules);
        Assert.Empty(db.ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted));
    }

    [Fact]
    public async Task GetPlanningDataAsync_IncludesOnlyActiveFutureSchedulesAndDoesNotMutateSources()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var cash = new BasicFinance.Web.Models.Account
        {
            Name = "Cash", Type = BasicFinance.Web.Models.AccountType.Checking,
            CurrencyCode = "USD", OpeningBalance = 500m
        };
        var goal = new BasicFinance.Web.Models.SavingsGoal
        {
            Name = "Holiday", TargetAmount = 2000m, CurrencyCode = "USD",
            Status = BasicFinance.Web.Models.SavingsGoalStatus.Active
        };
        db.Accounts.Add(cash);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();

        db.RecurringSavingsContributions.AddRange(
            new BasicFinance.Web.Models.RecurringSavingsContribution
            {
                SavingsGoalId = goal.Id, Amount = 100m,
                Frequency = BasicFinance.Web.Models.SavingsScheduleFrequency.Monthly,
                NextDueDate = new DateTime(2026, 12, 5), IsActive = true
            },
            new BasicFinance.Web.Models.RecurringSavingsContribution
            {
                SavingsGoalId = goal.Id, Amount = 75m,
                Frequency = BasicFinance.Web.Models.SavingsScheduleFrequency.Monthly,
                NextDueDate = new DateTime(2026, 12, 5), IsActive = false
            },
            new BasicFinance.Web.Models.RecurringSavingsContribution
            {
                SavingsGoalId = goal.Id, Amount = 50m,
                Frequency = BasicFinance.Web.Models.SavingsScheduleFrequency.Monthly,
                NextDueDate = new DateTime(2026, 12, 5), EndDate = new DateTime(2026, 12, 4), IsActive = true
            });
        db.RecurringCashFlows.AddRange(
            new BasicFinance.Web.Models.RecurringCashFlow
            {
                AccountId = cash.Id, Type = BasicFinance.Web.Models.TransactionType.Income,
                Description = "Pay", Amount = 3000m, CurrencyCode = "USD",
                Frequency = BasicFinance.Web.Models.CashFlowFrequency.Biweekly,
                NextDueDate = new DateTime(2026, 12, 1), IsActive = true
            },
            new BasicFinance.Web.Models.RecurringCashFlow
            {
                AccountId = cash.Id, Type = BasicFinance.Web.Models.TransactionType.Expense,
                Description = "Inactive", Amount = 200m, CurrencyCode = "USD",
                Frequency = BasicFinance.Web.Models.CashFlowFrequency.Monthly,
                NextDueDate = new DateTime(2026, 12, 1), IsActive = false
            },
            new BasicFinance.Web.Models.RecurringCashFlow
            {
                AccountId = cash.Id, Type = BasicFinance.Web.Models.TransactionType.Expense,
                Description = "Expired", Amount = 200m, CurrencyCode = "USD",
                Frequency = BasicFinance.Web.Models.CashFlowFrequency.Monthly,
                NextDueDate = new DateTime(2026, 12, 1), EndDate = new DateTime(2026, 11, 30), IsActive = true
            });
        await db.SaveChangesAsync();

        var provider = new FinancialPlanningDataProvider(db, new AccountBalanceService(db));
        var result = await provider.GetPlanningDataAsync(new DateOnly(2026, 11, 27), "USD");

        var savingsSchedule = Assert.Single(result.FutureSavingsSchedules);
        Assert.Equal(100m, savingsSchedule.Amount);
        Assert.Equal("Holiday", savingsSchedule.GoalName);
        var cashSchedule = Assert.Single(result.FutureCashFlowSchedules);
        Assert.Equal(3000m, cashSchedule.Amount);
        Assert.Equal(BasicFinance.Web.Models.TransactionType.Income, cashSchedule.Type);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

}
