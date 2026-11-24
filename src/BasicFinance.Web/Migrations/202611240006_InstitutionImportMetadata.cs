using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240006_InstitutionImportMetadata")]
public sealed class InstitutionImportMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Profile", table: "StatementImports", type: "INTEGER", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<string>(
            name: "AccountKey", table: "StatementImports", type: "TEXT", maxLength: 64, nullable: true);

        foreach (var (name, type, maxLength) in new (string Name, string Type, int? MaxLength)[]
        {
            ("SourceType", "TEXT", 100), ("SourceStatus", "TEXT", 100),
            ("SourceCategory", "TEXT", 100), ("SourceReference", "TEXT", 120),
            ("Instrument", "TEXT", 160), ("Memo", "TEXT", 200),
            ("CheckNumber", "TEXT", 80), ("ReferenceNumber", "TEXT", 120),
            ("TransactionCode", "TEXT", 120)
        })
        {
            migrationBuilder.AddColumn<string>(
                name: name, table: "StatementImportRows", type: type,
                maxLength: maxLength, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: name, table: "Transactions", type: type,
                maxLength: maxLength, nullable: true);
        }

        foreach (var column in new[]
        {
            "PostingDate", "EffectiveDate", "ActivityDate", "ProcessDate", "SettlementDate"
        })
        {
            migrationBuilder.AddColumn<DateTime>(
                name: column, table: "StatementImportRows", type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: column, table: "Transactions", type: "TEXT", nullable: true);
        }

        migrationBuilder.AddColumn<decimal>(name: "SourceSignedAmount", table: "StatementImportRows", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "RunningBalance", table: "StatementImportRows", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "DuplicateFingerprint", table: "StatementImportRows", type: "TEXT", maxLength: 64, nullable: true);
        migrationBuilder.AddColumn<bool>(name: "RequiresReview", table: "StatementImportRows", type: "INTEGER", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "SourceFieldsJson", table: "StatementImportRows", type: "TEXT", nullable: false, defaultValue: "{}");

        migrationBuilder.AddColumn<decimal>(name: "SourceSignedAmount", table: "Transactions", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "RunningBalance", table: "Transactions", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ImportFingerprint", table: "Transactions", type: "TEXT", maxLength: 64, nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatementImportRows_DuplicateFingerprint",
            table: "StatementImportRows",
            column: "DuplicateFingerprint");
        migrationBuilder.CreateIndex(
            name: "IX_Transactions_ImportFingerprint",
            table: "Transactions",
            column: "ImportFingerprint",
            unique: true,
            filter: "\"ImportFingerprint\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_StatementImportRows_DuplicateFingerprint", table: "StatementImportRows");
        migrationBuilder.DropIndex(name: "IX_Transactions_ImportFingerprint", table: "Transactions");

        foreach (var name in new[]
        {
            "SourceType", "SourceStatus", "SourceCategory", "SourceReference", "Instrument",
            "Memo", "CheckNumber", "ReferenceNumber", "TransactionCode"
        })
        {
            migrationBuilder.DropColumn(name: name, table: "StatementImportRows");
            migrationBuilder.DropColumn(name: name, table: "Transactions");
        }

        foreach (var name in new[]
        {
            "PostingDate", "EffectiveDate", "ActivityDate", "ProcessDate", "SettlementDate"
        })
        {
            migrationBuilder.DropColumn(name: name, table: "StatementImportRows");
            migrationBuilder.DropColumn(name: name, table: "Transactions");
        }

        migrationBuilder.DropColumn(name: "Profile", table: "StatementImports");
        migrationBuilder.DropColumn(name: "AccountKey", table: "StatementImports");
        migrationBuilder.DropColumn(name: "SourceSignedAmount", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "RunningBalance", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "DuplicateFingerprint", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "RequiresReview", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "SourceFieldsJson", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "SourceSignedAmount", table: "Transactions");
        migrationBuilder.DropColumn(name: "RunningBalance", table: "Transactions");
        migrationBuilder.DropColumn(name: "ImportFingerprint", table: "Transactions");
    }
}
