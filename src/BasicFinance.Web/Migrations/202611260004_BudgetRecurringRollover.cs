using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260004_BudgetRecurringRollover")]
public sealed class BudgetRecurringRollover : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsRecurring", table: "BudgetPeriods", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(
            name: "RolloverEnabled", table: "BudgetPeriods", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<decimal>(
            name: "RolloverAmount", table: "BudgetLines", type: "TEXT", nullable: false, defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RolloverAmount", table: "BudgetLines");
        migrationBuilder.DropColumn(name: "RolloverEnabled", table: "BudgetPeriods");
        migrationBuilder.DropColumn(name: "IsRecurring", table: "BudgetPeriods");
    }
}
