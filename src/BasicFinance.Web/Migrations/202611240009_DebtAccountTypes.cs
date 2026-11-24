using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240009_DebtAccountTypes")]
public sealed class DebtAccountTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DebtType",
            table: "Accounts",
            type: "INTEGER",
            nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_Accounts_DebtType",
            table: "Accounts",
            column: "DebtType");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Accounts_DebtType", table: "Accounts");
        migrationBuilder.DropColumn(name: "DebtType", table: "Accounts");
    }
}
