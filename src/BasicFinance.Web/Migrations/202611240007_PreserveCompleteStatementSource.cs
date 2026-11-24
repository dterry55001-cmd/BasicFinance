using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240007_PreserveCompleteStatementSource")]
public sealed class PreserveCompleteStatementSource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "StatementImportRows", "Transactions" })
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceSubType", table: table, type: "TEXT", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ExtendedDescription", table: table, type: "TEXT", maxLength: 200, nullable: true);
        }

        migrationBuilder.AddColumn<string>(
            name: "SourceFieldsJson", table: "Transactions", type: "TEXT",
            nullable: false, defaultValue: "{}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SourceSubType", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "ExtendedDescription", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "SourceSubType", table: "Transactions");
        migrationBuilder.DropColumn(name: "ExtendedDescription", table: "Transactions");
        migrationBuilder.DropColumn(name: "SourceFieldsJson", table: "Transactions");
    }
}
