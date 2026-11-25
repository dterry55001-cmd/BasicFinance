using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260002_BudgetManagementFoundation")]
public sealed class BudgetManagementFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BudgetPeriods",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                Year = table.Column<int>(type: "INTEGER", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_BudgetPeriods", x => x.Id));

        migrationBuilder.CreateTable(
            name: "BudgetLines",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                BudgetPeriodId = table.Column<int>(type: "INTEGER", nullable: false),
                CategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                PlannedAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BudgetLines", x => x.Id);
                table.ForeignKey("FK_BudgetLines_BudgetPeriods_BudgetPeriodId", x => x.BudgetPeriodId, "BudgetPeriods", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_BudgetLines_TransactionCategories_CategoryId", x => x.CategoryId, "TransactionCategories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_BudgetPeriods_Year_Month_CurrencyCode", "BudgetPeriods", new[] { "Year", "Month", "CurrencyCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_BudgetLines_BudgetPeriodId_CategoryId", "BudgetLines", new[] { "BudgetPeriodId", "CategoryId" }, unique: true);
        migrationBuilder.CreateIndex("IX_BudgetLines_CategoryId", "BudgetLines", "CategoryId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("BudgetLines");
        migrationBuilder.DropTable("BudgetPeriods");
    }
}
