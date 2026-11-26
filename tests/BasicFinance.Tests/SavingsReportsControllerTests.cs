using BasicFinance.Web.Controllers;
using BasicFinance.Web.Data;
using BasicFinance.Web.Models;
using BasicFinance.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Xunit;

namespace BasicFinance.Tests;

public sealed class SavingsReportsControllerTests
{
    [Fact]
    public async Task Index_ComparesMonthlyActualsWithTargetsAndBuildsAnnualSummary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        var goal = new SavingsGoal
        {
            Name = "Emergency reserve",
            TargetAmount = 5000m,
            CurrencyCode = "USD",
            StartDate = new DateTime(2026, 1, 1),
            Status = SavingsGoalStatus.Active,
            IsEmergencyFund = true,
            Contributions =
            [
                new SavingsContribution { Amount = 400m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 11, 5) },
                new SavingsContribution { Amount = 75m, Type = SavingsContributionType.Withdrawal, ContributionDate = new DateTime(2026, 11, 10) },
                new SavingsContribution { Amount = 200m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 10, 5) }
            ],
            MonthlyTargets =
            [
                new SavingsMonthlyTarget { Year = 2026, Month = 11, TargetAmount = 500m },
                new SavingsMonthlyTarget { Year = 2026, Month = 10, TargetAmount = 300m }
            ]
        };
        database.SavingsGoals.Add(goal);
        await database.SaveChangesAsync();

        var controller = new SavingsReportsController(database, new SavingsGoalService());
        var result = await controller.Index(2026, 11, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SavingsReportsViewModel>(view.Model);
        var row = Assert.Single(model.GoalRows);

        Assert.Equal(500m, row.PlannedTarget);
        Assert.Equal(400m, row.Contributions);
        Assert.Equal(75m, row.Withdrawals);
        Assert.Equal(325m, row.NetSavings);
        Assert.Equal(-100m, row.Variance);
        Assert.Equal(525m, row.ClosingBalance);
        Assert.Equal(500m, model.AnnualMonths.Single(x => x.Month.Month == 11).PlannedTarget);
        Assert.Equal(400m, model.AnnualMonths.Single(x => x.Month.Month == 11).Contributions);
        Assert.Equal(325m, model.AnnualMonths.Single(x => x.Month.Month == 11).NetSavings);
    }

    [Fact]
    public async Task Export_ReturnsCsvWithEscapedGoalNamesAndFinancialValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var database = new ApplicationDbContext(options);
        await database.Database.MigrateAsync();

        database.SavingsGoals.Add(new SavingsGoal
        {
            Name = "Family \"Travel\" Fund",
            TargetAmount = 2000m,
            CurrencyCode = "USD",
            StartDate = new DateTime(2026, 1, 1),
            Status = SavingsGoalStatus.Active,
            Contributions =
            [
                new SavingsContribution { Amount = 125m, Type = SavingsContributionType.Contribution, ContributionDate = new DateTime(2026, 11, 2) }
            ]
        });
        await database.SaveChangesAsync();

        var controller = new SavingsReportsController(database, new SavingsGoalService());
        var result = await controller.Export(2026, 11, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        var csv = Encoding.UTF8.GetString(file.FileContents);
        Assert.Contains("\"Family \"\"Travel\"\" Fund\"", csv);
        Assert.Contains("125.00", csv);
    }
}
