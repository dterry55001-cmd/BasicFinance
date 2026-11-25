using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240014_DebtPaymentTracking")]
public sealed class DebtPaymentTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "DebtPaymentRecords" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_DebtPaymentRecords" PRIMARY KEY AUTOINCREMENT,
                "AccountId" INTEGER NOT NULL,
                "DueDate" TEXT NOT NULL,
                "ExpectedAmount" TEXT NOT NULL,
                "Status" INTEGER NOT NULL,
                "PaidDate" TEXT NULL,
                "ActualAmount" TEXT NULL,
                "PrincipalPaid" TEXT NULL,
                "InterestPaid" TEXT NULL,
                "FeesPaid" TEXT NULL,
                "SourceAccountId" INTEGER NULL,
                "TransactionId" INTEGER NULL,
                "Notes" TEXT NULL,
                "CreatedUtc" TEXT NOT NULL,
                "UpdatedUtc" TEXT NOT NULL,
                CONSTRAINT "FK_DebtPaymentRecords_Accounts_AccountId"
                    FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_DebtPaymentRecords_Accounts_SourceAccountId"
                    FOREIGN KEY ("SourceAccountId") REFERENCES "Accounts" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_DebtPaymentRecords_Transactions_TransactionId"
                    FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("Id") ON DELETE SET NULL
            );
            CREATE UNIQUE INDEX "IX_DebtPaymentRecords_AccountId_DueDate"
                ON "DebtPaymentRecords" ("AccountId", "DueDate");
            CREATE INDEX "IX_DebtPaymentRecords_Status_DueDate"
                ON "DebtPaymentRecords" ("Status", "DueDate");
            CREATE UNIQUE INDEX "IX_DebtPaymentRecords_TransactionId"
                ON "DebtPaymentRecords" ("TransactionId");
            CREATE INDEX "IX_DebtPaymentRecords_SourceAccountId"
                ON "DebtPaymentRecords" ("SourceAccountId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DebtPaymentRecords");
    }
}
