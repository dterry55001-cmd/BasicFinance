using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240001_InitialAccounts")]
public sealed class InitialAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Institution = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                AccountNumberLast4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: true),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                OpeningBalance = table.Column<string>(type: "TEXT", nullable: false),
                OpenedOn = table.Column<DateTime>(type: "TEXT", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Accounts", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Accounts_Status",
            table: "Accounts",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Accounts");
    }
}
