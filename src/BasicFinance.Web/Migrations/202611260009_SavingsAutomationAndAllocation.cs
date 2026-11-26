using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260009_SavingsAutomationAndAllocation")]
public sealed class SavingsAutomationAndAllocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RecurringSavingsContributions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                SavingsGoalId = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                NextDueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecurringSavingsContributions", x => x.Id);
                table.ForeignKey("FK_RecurringSavingsContributions_SavingsGoals_SavingsGoalId",
                    x => x.SavingsGoalId, "SavingsGoals", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SavingsAllocationBudgets",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                MonthlyBudget = table.Column<decimal>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SavingsAllocationBudgets", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SavingsAllocationSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                SavingsGoalId = table.Column<int>(type: "INTEGER", nullable: false),
                AllocationPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsAllocationSettings", x => x.Id);
                table.ForeignKey("FK_SavingsAllocationSettings_SavingsGoals_SavingsGoalId",
                    x => x.SavingsGoalId, "SavingsGoals", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_RecurringSavingsContributions_IsActive_NextDueDate",
            "RecurringSavingsContributions", new[] { "IsActive", "NextDueDate" });
        migrationBuilder.CreateIndex("IX_RecurringSavingsContributions_SavingsGoalId",
            "RecurringSavingsContributions", "SavingsGoalId");
        migrationBuilder.CreateIndex("IX_SavingsAllocationSettings_SavingsGoalId",
            "SavingsAllocationSettings", "SavingsGoalId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SavingsAllocationSettings");
        migrationBuilder.DropTable("SavingsAllocationBudgets");
        migrationBuilder.DropTable("RecurringSavingsContributions");
    }
}
