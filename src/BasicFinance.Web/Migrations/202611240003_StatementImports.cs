using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240003_StatementImports")]
public sealed class StatementImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StatementImports",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                CompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatementImports", x => x.Id);
                table.ForeignKey("FK_StatementImports_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StatementImportRows",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                StatementImportId = table.Column<int>(type: "INTEGER", nullable: false),
                RowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                ValidationError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                IsDuplicate = table.Column<bool>(type: "INTEGER", nullable: false),
                IsImported = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatementImportRows", x => x.Id);
                table.ForeignKey("FK_StatementImportRows_StatementImports_StatementImportId", x => x.StatementImportId, "StatementImports", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_StatementImports_AccountId", table: "StatementImports", column: "AccountId");
        migrationBuilder.CreateIndex(name: "IX_StatementImports_CreatedUtc", table: "StatementImports", column: "CreatedUtc");
        migrationBuilder.CreateIndex(name: "IX_StatementImportRows_StatementImportId_RowNumber", table: "StatementImportRows", columns: new[] { "StatementImportId", "RowNumber" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StatementImportRows");
        migrationBuilder.DropTable(name: "StatementImports");
    }
}
