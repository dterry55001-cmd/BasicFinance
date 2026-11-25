using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240017_RepeatedDebtFreeMilestones")]
public sealed class RepeatedDebtFreeMilestones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_DebtFreeMilestones_AccountId";
            CREATE INDEX "IX_DebtFreeMilestones_AccountId_AchievedDate"
                ON "DebtFreeMilestones" ("AccountId", "AchievedDate");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "DebtFreeMilestones"
            WHERE "Id" NOT IN (
                SELECT MAX("Id") FROM "DebtFreeMilestones" GROUP BY "AccountId"
            );
            DROP INDEX IF EXISTS "IX_DebtFreeMilestones_AccountId_AchievedDate";
            CREATE UNIQUE INDEX "IX_DebtFreeMilestones_AccountId"
                ON "DebtFreeMilestones" ("AccountId");
            """);
    }
}