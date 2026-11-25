using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240013_DebtToIncomeHistory")]
public sealed class DebtToIncomeHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "DebtToIncomeRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DebtToIncomeRecords" PRIMARY KEY AUTOINCREMENT,
                "Month" TEXT NOT NULL,
                "CurrencyCode" TEXT NOT NULL,
                "GrossMonthlyIncome" TEXT NOT NULL,
                "HousingMonthlyPayment" TEXT NOT NULL,
                "OtherMonthlyDebtPayment" TEXT NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX "IX_DebtToIncomeRecords_Month_CurrencyCode"
                ON "DebtToIncomeRecords" ("Month", "CurrencyCode");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DebtToIncomeRecords");
    }
}
