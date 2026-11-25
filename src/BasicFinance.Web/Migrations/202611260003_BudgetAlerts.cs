using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260003_BudgetAlerts")]
public sealed class BudgetAlerts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BudgetAlertPreferences",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BudgetAlertPreferences", x => x.Id));

        migrationBuilder.CreateTable(
            name: "BudgetAlerts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                BudgetPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                ThresholdPercent = table.Column<int>(type: "INTEGER", nullable: false),
                ActualAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsRead = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BudgetAlerts", x => x.Id);
                table.ForeignKey("FK_BudgetAlerts_BudgetPeriods_BudgetPeriodId", x => x.BudgetPeriodId, "BudgetPeriods", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_BudgetAlerts_TransactionCategories_CategoryId", x => x.CategoryId, "TransactionCategories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_BudgetAlerts_BudgetPeriodId_CategoryId_ThresholdPercent", "BudgetAlerts", new[] { "BudgetPeriodId", "CategoryId", "ThresholdPercent" }, unique: true);
        migrationBuilder.CreateIndex("IX_BudgetAlerts_CategoryId", "BudgetAlerts", "CategoryId");
        migrationBuilder.CreateIndex("IX_BudgetAlerts_IsRead_CreatedUtc", "BudgetAlerts", new[] { "IsRead", "CreatedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("BudgetAlerts");
        migrationBuilder.DropTable("BudgetAlertPreferences");
    }
}
