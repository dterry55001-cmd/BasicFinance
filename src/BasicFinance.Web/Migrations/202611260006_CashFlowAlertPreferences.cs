using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260006_CashFlowAlertPreferences")]
public sealed class CashFlowAlertPreferences : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CashFlowAlertPreferences",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                LowBalanceThreshold = table.Column<decimal>(type: "TEXT", nullable: false),
                AlertsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CashFlowAlertPreferences", x => x.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("CashFlowAlertPreferences");
}
