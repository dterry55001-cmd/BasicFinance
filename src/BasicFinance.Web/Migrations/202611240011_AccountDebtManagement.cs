using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240011_AccountDebtManagement")]
public sealed class AccountDebtManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "AccountBalanceSnapshots" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_AccountBalanceSnapshots" PRIMARY KEY AUTOINCREMENT,
                "AccountId" INTEGER NOT NULL,
                "SnapshotDate" TEXT NOT NULL,
                "Balance" TEXT NOT NULL,
                "CreatedUtc" TEXT NOT NULL,
                CONSTRAINT "FK_AccountBalanceSnapshots_Accounts_AccountId"
                    FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX "IX_AccountBalanceSnapshots_AccountId_SnapshotDate"
                ON "AccountBalanceSnapshots" ("AccountId", "SnapshotDate");

            CREATE TABLE "InterestRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_InterestRecords" PRIMARY KEY AUTOINCREMENT,
                "AccountId" INTEGER NOT NULL,
                "TransactionId" INTEGER NULL,
                "InterestDate" TEXT NOT NULL,
                "Type" INTEGER NOT NULL,
                "Amount" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Notes" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                CONSTRAINT "FK_InterestRecords_Accounts_AccountId"
                    FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_InterestRecords_Transactions_TransactionId"
                    FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX "IX_InterestRecords_AccountId_InterestDate"
                ON "InterestRecords" ("AccountId", "InterestDate");
            CREATE INDEX "IX_InterestRecords_TransactionId"
                ON "InterestRecords" ("TransactionId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InterestRecords");
        migrationBuilder.DropTable(name: "AccountBalanceSnapshots");
    }
}
