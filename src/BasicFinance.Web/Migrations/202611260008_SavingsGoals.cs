using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260008_SavingsGoals")]
public sealed class SavingsGoals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SavingsGoals",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                TargetAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                TargetDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                IsEmergencyFund = table.Column<bool>(type: "INTEGER", nullable: false),
                Priority = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SavingsGoals", x => x.Id));

        migrationBuilder.CreateTable(
            name: "EmergencyFundPlans",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                MonthlyEssentialExpenses = table.Column<decimal>(type: "TEXT", nullable: false),
                TargetCoverageMonths = table.Column<int>(type: "INTEGER", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EmergencyFundPlans", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SavingsContributions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                SavingsGoalId = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                ContributionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                TransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsContributions", x => x.Id);
                table.ForeignKey("FK_SavingsContributions_SavingsGoals_SavingsGoalId", x => x.SavingsGoalId, "SavingsGoals", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_SavingsContributions_Transactions_TransactionId", x => x.TransactionId, "Transactions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SavingsMonthlyTargets",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                SavingsGoalId = table.Column<int>(type: "INTEGER", nullable: false),
                Year = table.Column<int>(type: "INTEGER", nullable: false),
                Month = table.Column<int>(type: "INTEGER", nullable: false),
                TargetAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsMonthlyTargets", x => x.Id);
                table.ForeignKey("FK_SavingsMonthlyTargets_SavingsGoals_SavingsGoalId", x => x.SavingsGoalId, "SavingsGoals", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SavingsGoalMilestones",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                SavingsGoalId = table.Column<int>(type: "INTEGER", nullable: false),
                MilestonePercent = table.Column<int>(type: "INTEGER", nullable: false),
                AchievedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsGoalMilestones", x => x.Id);
                table.ForeignKey("FK_SavingsGoalMilestones_SavingsGoals_SavingsGoalId", x => x.SavingsGoalId, "SavingsGoals", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_SavingsGoals_Status_Priority", "SavingsGoals", new[] { "Status", "Priority" });
        migrationBuilder.CreateIndex("IX_SavingsGoals_IsEmergencyFund", "SavingsGoals", "IsEmergencyFund", unique: true, filter: "\"IsEmergencyFund\" = 1");
        migrationBuilder.CreateIndex("IX_SavingsContributions_SavingsGoalId_ContributionDate", "SavingsContributions", new[] { "SavingsGoalId", "ContributionDate" });
        migrationBuilder.CreateIndex("IX_SavingsContributions_TransactionId", "SavingsContributions", "TransactionId", unique: true, filter: "\"TransactionId\" IS NOT NULL");
        migrationBuilder.CreateIndex("IX_SavingsMonthlyTargets_SavingsGoalId_Year_Month", "SavingsMonthlyTargets", new[] { "SavingsGoalId", "Year", "Month" }, unique: true);
        migrationBuilder.CreateIndex("IX_SavingsGoalMilestones_SavingsGoalId_MilestonePercent", "SavingsGoalMilestones", new[] { "SavingsGoalId", "MilestonePercent" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SavingsGoalMilestones");
        migrationBuilder.DropTable("SavingsMonthlyTargets");
        migrationBuilder.DropTable("SavingsContributions");
        migrationBuilder.DropTable("EmergencyFundPlans");
        migrationBuilder.DropTable("SavingsGoals");
    }
}
