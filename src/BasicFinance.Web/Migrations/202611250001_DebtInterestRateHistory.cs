using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611250001_DebtInterestRateHistory")]
public sealed class DebtInterestRateHistoryMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DebtInterestRateHistory",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                AnnualRatePercent = table.Column<decimal>(type: "TEXT", nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                RateType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                PromotionalEndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                RecordedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DebtInterestRateHistory", x => x.Id);
                table.ForeignKey("FK_DebtInterestRateHistory_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_DebtInterestRateHistory_AccountId_EffectiveDate",
            table: "DebtInterestRateHistory", columns: new[] { "AccountId", "EffectiveDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "DebtInterestRateHistory");
}
