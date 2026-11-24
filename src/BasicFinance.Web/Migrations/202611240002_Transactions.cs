using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240002_Transactions")]
public sealed class Transactions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Transactions",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                RelatedAccountId = table.Column<int>(type: "INTEGER", nullable: true),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Description = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                CurrencyCode = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                TransactionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transactions", x => x.Id);
                table.ForeignKey("FK_Transactions_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Transactions_Accounts_RelatedAccountId", x => x.RelatedAccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_Transactions_AccountId", table: "Transactions", column: "AccountId");
        migrationBuilder.CreateIndex(name: "IX_Transactions_RelatedAccountId", table: "Transactions", column: "RelatedAccountId");
        migrationBuilder.CreateIndex(name: "IX_Transactions_TransactionDate", table: "Transactions", column: "TransactionDate");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Transactions");
    }
}
