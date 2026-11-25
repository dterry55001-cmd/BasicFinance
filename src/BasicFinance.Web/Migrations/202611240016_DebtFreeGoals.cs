using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240016_DebtFreeGoals")]
public sealed class DebtFreeGoals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "DebtFreeTargets" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DebtFreeTargets" PRIMARY KEY AUTOINCREMENT,
                "CurrencyCode" TEXT NOT NULL,
                "TargetDate" TEXT NOT NULL,
                "ExtraMonthlyPayment" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_DebtFreeTargets_CurrencyCode" ON "DebtFreeTargets" ("CurrencyCode");

            CREATE TABLE "MonthlyDebtReductionGoals" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_MonthlyDebtReductionGoals" PRIMARY KEY AUTOINCREMENT,
                "CurrencyCode" TEXT NOT NULL,
                "Month" TEXT NOT NULL,
                "TargetReduction" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_MonthlyDebtReductionGoals_CurrencyCode_Month"
                ON "MonthlyDebtReductionGoals" ("CurrencyCode", "Month");

            CREATE TABLE "DebtFreeMilestones" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DebtFreeMilestones" PRIMARY KEY AUTOINCREMENT,
                "AccountId" INTEGER NOT NULL,
                "AchievedDate" TEXT NOT NULL,
                "IsReopened" INTEGER NOT NULL,
                "ReopenedDate" TEXT NULL,
                "Notes" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                CONSTRAINT "FK_DebtFreeMilestones_Accounts_AccountId"
                    FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_DebtFreeMilestones_AccountId" ON "DebtFreeMilestones" ("AccountId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("DebtFreeMilestones");
        migrationBuilder.DropTable("MonthlyDebtReductionGoals");
        migrationBuilder.DropTable("DebtFreeTargets");
    }
}
