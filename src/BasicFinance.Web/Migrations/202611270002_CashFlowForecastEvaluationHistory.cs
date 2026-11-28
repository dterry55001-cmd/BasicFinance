using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611270002_CashFlowForecastEvaluationHistory")]
public sealed class CashFlowForecastEvaluationHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CashFlowForecastEvaluations",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                ForecastSnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                EvaluatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ActualDataComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                ResultsJson = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CashFlowForecastEvaluations", x => x.Id);
                table.ForeignKey("FK_CashFlowForecastEvaluations_CashFlowForecastSnapshots_ForecastSnapshotId",
                    x => x.ForecastSnapshotId, "CashFlowForecastSnapshots", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_CashFlowForecastEvaluations_ForecastSnapshotId_EvaluatedUtc",
            "CashFlowForecastEvaluations", new[] { "ForecastSnapshotId", "EvaluatedUtc" });
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("CashFlowForecastEvaluations");
}