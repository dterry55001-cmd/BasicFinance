using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611270001_ImmutableCashFlowForecastSnapshots")]
public sealed class ImmutableCashFlowForecastSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CashFlowForecastSnapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ForecastStartMonth = table.Column<DateTime>(type: "TEXT", nullable: false),
                ForecastMonths = table.Column<int>(type: "INTEGER", nullable: false),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                OpeningBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                CalculationVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                AssumptionsJson = table.Column<string>(type: "TEXT", nullable: false),
                ResultsJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CashFlowForecastSnapshots", x => x.Id));
        migrationBuilder.CreateIndex("IX_CashFlowForecastSnapshots_CreatedUtc", "CashFlowForecastSnapshots", "CreatedUtc");
        migrationBuilder.CreateIndex("IX_CashFlowForecastSnapshots_ForecastStartMonth_CurrencyCode",
            "CashFlowForecastSnapshots", new[] { "ForecastStartMonth", "CurrencyCode" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("CashFlowForecastSnapshots");
}