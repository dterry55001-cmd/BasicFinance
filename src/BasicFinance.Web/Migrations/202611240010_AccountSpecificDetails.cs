using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240010_AccountSpecificDetails")]
public sealed class AccountSpecificDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // EF Core's SQLite migration generator does not emit DropColumn directly.
        // SQLite 3.35+ supports this operation natively; Microsoft.Data.Sqlite
        // ships a compatible SQLite engine with the application's .NET 10 stack.
        migrationBuilder.Sql("ALTER TABLE \"Accounts\" DROP COLUMN \"AccountNumberLast4\";");
        migrationBuilder.AddColumn<string>(name: "InterestRatePercent", table: "Accounts", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "CreditLimit", table: "Accounts", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "MinimumPayment", table: "Accounts", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>(name: "PaymentDueDay", table: "Accounts", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<string>(name: "OriginalLoanAmount", table: "Accounts", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<int>(name: "LoanTermMonths", table: "Accounts", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "MaturityDate", table: "Accounts", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "EmployerName", table: "Accounts", type: "TEXT", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "EmployerMatchPercent", table: "Accounts", type: "TEXT", nullable: true);
        migrationBuilder.AddColumn<string>(name: "TaxTreatment", table: "Accounts", type: "TEXT", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<string>(name: "BrokerageRegistrationType", table: "Accounts", type: "TEXT", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(name: "Notes", table: "Accounts", type: "TEXT", maxLength: 1000, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "InterestRatePercent", table: "Accounts");
        migrationBuilder.DropColumn(name: "CreditLimit", table: "Accounts");
        migrationBuilder.DropColumn(name: "MinimumPayment", table: "Accounts");
        migrationBuilder.DropColumn(name: "PaymentDueDay", table: "Accounts");
        migrationBuilder.DropColumn(name: "OriginalLoanAmount", table: "Accounts");
        migrationBuilder.DropColumn(name: "LoanTermMonths", table: "Accounts");
        migrationBuilder.DropColumn(name: "MaturityDate", table: "Accounts");
        migrationBuilder.DropColumn(name: "EmployerName", table: "Accounts");
        migrationBuilder.DropColumn(name: "EmployerMatchPercent", table: "Accounts");
        migrationBuilder.DropColumn(name: "TaxTreatment", table: "Accounts");
        migrationBuilder.DropColumn(name: "BrokerageRegistrationType", table: "Accounts");
        migrationBuilder.DropColumn(name: "Notes", table: "Accounts");
    }
}
