using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240018_PersistentDebtNotifications")]
public sealed class PersistentDebtNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DebtNotifications",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                NotificationKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Level = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                EventDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                IsRead = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_DebtNotifications", x => x.Id));
        migrationBuilder.CreateIndex(name: "IX_DebtNotifications_NotificationKey", table: "DebtNotifications", column: "NotificationKey", unique: true);
        migrationBuilder.CreateIndex(name: "IX_DebtNotifications_IsRead_CreatedUtc", table: "DebtNotifications", columns: new[] { "IsRead", "CreatedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DebtNotifications");
    }
}