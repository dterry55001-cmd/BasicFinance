using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240008_RetirementAccountTypes")]
public sealed class RetirementAccountTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RetirementType",
            table: "Accounts",
            type: "INTEGER",
            nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_Accounts_RetirementType",
            table: "Accounts",
            column: "RetirementType");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Accounts_RetirementType", table: "Accounts");
        migrationBuilder.DropColumn(name: "RetirementType", table: "Accounts");
    }
}
