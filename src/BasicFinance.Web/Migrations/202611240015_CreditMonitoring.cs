using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240015_CreditMonitoring")]
public sealed class CreditMonitoring : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CreditLimitHistory",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                PreviousLimit = table.Column<decimal>(type: "TEXT", nullable: true),
                NewLimit = table.Column<decimal>(type: "TEXT", nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Source = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                RecordedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CreditLimitHistory", x => x.Id);
                table.ForeignKey("FK_CreditLimitHistory_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ReportedBalanceSnapshots",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                ReportedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ReportedBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                ReportedCreditLimit = table.Column<decimal>(type: "TEXT", nullable: true),
                Source = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                ReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReportedBalanceSnapshots", x => x.Id);
                table.ForeignKey("FK_ReportedBalanceSnapshots_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CreditInquiries",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                InquiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Bureau = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                Lender = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CreditInquiries", x => x.Id));

        migrationBuilder.CreateIndex("IX_CreditLimitHistory_AccountId_EffectiveDate", "CreditLimitHistory", new[] { "AccountId", "EffectiveDate" });
        migrationBuilder.CreateIndex("IX_ReportedBalanceSnapshots_AccountId_ReportedDate_Source", "ReportedBalanceSnapshots", new[] { "AccountId", "ReportedDate", "Source" }, unique: true);
        migrationBuilder.CreateIndex("IX_CreditInquiries_InquiryDate", "CreditInquiries", "InquiryDate");
        migrationBuilder.CreateIndex("IX_CreditInquiries_Bureau", "CreditInquiries", "Bureau");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("CreditInquiries");
        migrationBuilder.DropTable("ReportedBalanceSnapshots");
        migrationBuilder.DropTable("CreditLimitHistory");
    }
}
