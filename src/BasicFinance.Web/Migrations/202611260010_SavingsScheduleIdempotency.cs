using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260010_SavingsScheduleIdempotency")]
public sealed class SavingsScheduleIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RecurringSavingsContributionId",
            table: "SavingsContributions",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledDueDate",
            table: "SavingsContributions",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SavingsContributions_RecurringSavingsContributionId_ScheduledDueDate",
            table: "SavingsContributions",
            columns: new[] { "RecurringSavingsContributionId", "ScheduledDueDate" },
            unique: true,
            filter: "\"RecurringSavingsContributionId\" IS NOT NULL AND \"ScheduledDueDate\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SavingsContributions_RecurringSavingsContributionId_ScheduledDueDate",
            table: "SavingsContributions");
        migrationBuilder.DropColumn(name: "RecurringSavingsContributionId", table: "SavingsContributions");
        migrationBuilder.DropColumn(name: "ScheduledDueDate", table: "SavingsContributions");
    }
}
