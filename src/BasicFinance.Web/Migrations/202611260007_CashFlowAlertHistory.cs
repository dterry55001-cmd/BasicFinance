using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260007_CashFlowAlertHistory")]
public sealed class CashFlowAlertHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CashFlowAlerts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                ForecastDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ProjectedBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                ThresholdAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                AlertKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsRead = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CashFlowAlerts", x => x.Id));
        migrationBuilder.CreateIndex("IX_CashFlowAlerts_AlertKey", "CashFlowAlerts", "AlertKey", unique: true);
        migrationBuilder.CreateIndex("IX_CashFlowAlerts_IsRead_CreatedUtc", "CashFlowAlerts", new[] { "IsRead", "CreatedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("CashFlowAlerts");
}
