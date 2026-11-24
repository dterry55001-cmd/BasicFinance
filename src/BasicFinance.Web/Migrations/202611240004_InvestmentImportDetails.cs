using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240004_InvestmentImportDetails")]
public sealed class InvestmentImportDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Source", table: "Transactions", type: "TEXT", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "FundName", table: "Transactions", type: "TEXT", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UnitCount", table: "Transactions", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UnitValue", table: "Transactions", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Source", table: "StatementImportRows", type: "TEXT", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "FundName", table: "StatementImportRows", type: "TEXT", maxLength: 160, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UnitCount", table: "StatementImportRows", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UnitValue", table: "StatementImportRows", type: "TEXT", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Source", table: "Transactions");
        migrationBuilder.DropColumn(name: "FundName", table: "Transactions");
        migrationBuilder.DropColumn(name: "UnitCount", table: "Transactions");
        migrationBuilder.DropColumn(name: "UnitValue", table: "Transactions");
        migrationBuilder.DropColumn(name: "Source", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "FundName", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "UnitCount", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "UnitValue", table: "StatementImportRows");
    }
}
