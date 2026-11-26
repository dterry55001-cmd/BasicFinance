using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260012_SavingsReconciliation")]
public sealed class SavingsReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "LinkedAccountId",
            table: "SavingsGoals",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SavingsGoals_LinkedAccountId",
            table: "SavingsGoals",
            column: "LinkedAccountId",
            unique: true,
            filter: "\"LinkedAccountId\" IS NOT NULL AND \"Status\" <> 3");

        migrationBuilder.CreateTable(
            name: "SavingsReconciliationSessions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                SavingsGoalId = table.Column<int>(type: "INTEGER", nullable: false),
                AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                PeriodEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                StatementClosingBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                CalculatedAccountBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                Difference = table.Column<decimal>(type: "TEXT", nullable: false),
                MatchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                UnmatchedTransactionCount = table.Column<int>(type: "INTEGER", nullable: false),
                UnmatchedContributionCount = table.Column<int>(type: "INTEGER", nullable: false),
                PotentialDuplicateCount = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsReconciliationSessions", x => x.Id);
                table.ForeignKey("FK_SavingsReconciliationSessions_SavingsGoals_SavingsGoalId", x => x.SavingsGoalId, "SavingsGoals", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SavingsReconciliationSessions_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SavingsReconciliationItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                SavingsReconciliationSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                TransactionId = table.Column<int>(type: "INTEGER", nullable: true),
                SavingsContributionId = table.Column<int>(type: "INTEGER", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavingsReconciliationItems", x => x.Id);
                table.ForeignKey("FK_SavingsReconciliationItems_SavingsReconciliationSessions_SavingsReconciliationSessionId", x => x.SavingsReconciliationSessionId, "SavingsReconciliationSessions", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_SavingsReconciliationItems_Transactions_TransactionId", x => x.TransactionId, "Transactions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_SavingsReconciliationItems_SavingsContributions_SavingsContributionId", x => x.SavingsContributionId, "SavingsContributions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_SavingsReconciliationSessions_SavingsGoalId_AccountId_PeriodStart_PeriodEnd",
            "SavingsReconciliationSessions", new[] { "SavingsGoalId", "AccountId", "PeriodStart", "PeriodEnd" });
        migrationBuilder.CreateIndex("IX_SavingsReconciliationItems_SavingsReconciliationSessionId",
            "SavingsReconciliationItems", "SavingsReconciliationSessionId");
        migrationBuilder.CreateIndex("IX_SavingsReconciliationItems_TransactionId",
            "SavingsReconciliationItems", "TransactionId");
        migrationBuilder.CreateIndex("IX_SavingsReconciliationItems_SavingsContributionId",
            "SavingsReconciliationItems", "SavingsContributionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SavingsReconciliationItems");
        migrationBuilder.DropTable("SavingsReconciliationSessions");
        migrationBuilder.DropIndex("IX_SavingsGoals_LinkedAccountId", "SavingsGoals");
        migrationBuilder.DropColumn("LinkedAccountId", "SavingsGoals");
    }
}
