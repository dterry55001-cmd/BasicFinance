using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611260011_SavingsNotifications")]
public sealed class SavingsNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SavingsNotificationPreferences",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false),
                UpcomingContributionRemindersEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                MissedContributionAlertsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                MilestoneNotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                EmergencyFundWarningsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                MonthlyAllocationReviewsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                UpcomingReminderDays = table.Column<int>(type: "INTEGER", nullable: false),
                EmergencyFundWarningMonths = table.Column<decimal>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_SavingsNotificationPreferences", x => x.Id));

        migrationBuilder.CreateTable(
            name: "SavingsNotifications",
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
            constraints: table => table.PrimaryKey("PK_SavingsNotifications", x => x.Id));

        migrationBuilder.CreateIndex("IX_SavingsNotifications_NotificationKey", "SavingsNotifications", "NotificationKey", unique: true);
        migrationBuilder.CreateIndex("IX_SavingsNotifications_IsRead_CreatedUtc", "SavingsNotifications", new[] { "IsRead", "CreatedUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("SavingsNotifications");
        migrationBuilder.DropTable("SavingsNotificationPreferences");
    }
}
