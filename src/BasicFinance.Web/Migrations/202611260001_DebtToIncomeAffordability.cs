using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260001_DebtToIncomeAffordability")]
public sealed class DebtToIncomeAffordability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NetMonthlyIncome",
            table: "DebtToIncomeRecords",
            type: "TEXT",
            nullable: false,
            defaultValue: "0");

        migrationBuilder.AddColumn<string>(
            name: "OtherEssentialMonthlyExpenses",
            table: "DebtToIncomeRecords",
            type: "TEXT",
            nullable: false,
            defaultValue: "0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "NetMonthlyIncome", table: "DebtToIncomeRecords");
        migrationBuilder.DropColumn(name: "OtherEssentialMonthlyExpenses", table: "DebtToIncomeRecords");
    }
}
