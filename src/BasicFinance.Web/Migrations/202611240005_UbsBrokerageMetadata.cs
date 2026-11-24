using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240005_UbsBrokerageMetadata")]
public sealed class UbsBrokerageMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ExternalAccountNumberLast4", table: "StatementImports", type: "TEXT", maxLength: 4, nullable: true);
        migrationBuilder.AddColumn<string>(name: "FriendlyAccountName", table: "StatementImports", type: "TEXT", maxLength: 100, nullable: true);

        foreach (var table in new[] { "StatementImportRows", "Transactions" })
        {
            migrationBuilder.AddColumn<string>(name: "Activity", table: table, type: "TEXT", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Symbol", table: table, type: "TEXT", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Cusip", table: table, type: "TEXT", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<string>(name: "SecurityType", table: table, type: "TEXT", maxLength: 40, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Quantity", table: table, type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "Price", table: table, type: "TEXT", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ExternalAccountNumberLast4", table: table, type: "TEXT", maxLength: 4, nullable: true);
            migrationBuilder.AddColumn<string>(name: "FriendlyAccountName", table: table, type: "TEXT", maxLength: 100, nullable: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ExternalAccountNumberLast4", table: "StatementImports");
        migrationBuilder.DropColumn(name: "FriendlyAccountName", table: "StatementImports");
        foreach (var table in new[] { "StatementImportRows", "Transactions" })
        {
            migrationBuilder.DropColumn(name: "Activity", table: table);
            migrationBuilder.DropColumn(name: "Symbol", table: table);
            migrationBuilder.DropColumn(name: "Cusip", table: table);
            migrationBuilder.DropColumn(name: "SecurityType", table: table);
            migrationBuilder.DropColumn(name: "Quantity", table: table);
            migrationBuilder.DropColumn(name: "Price", table: table);
            migrationBuilder.DropColumn(name: "ExternalAccountNumberLast4", table: table);
            migrationBuilder.DropColumn(name: "FriendlyAccountName", table: table);
        }
    }
}
