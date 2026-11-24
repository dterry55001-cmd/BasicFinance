using BasicFinance.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BasicFinance.Web.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202611240012_TransactionCategoriesAndRules")]
public sealed class TransactionCategoriesAndRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TransactionCategories",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                ParentCategoryId = table.Column<int>(type: "INTEGER", nullable: true),
                Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransactionCategories", x => x.Id);
                table.ForeignKey("FK_TransactionCategories_TransactionCategories_ParentCategoryId",
                    x => x.ParentCategoryId, "TransactionCategories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TransactionCategoryMappings",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Profile = table.Column<int>(type: "INTEGER", nullable: false),
                SourceCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                NormalizedSourceCategory = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                TransactionCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransactionCategoryMappings", x => x.Id);
                table.ForeignKey("FK_TransactionCategoryMappings_TransactionCategories_TransactionCategoryId",
                    x => x.TransactionCategoryId, "TransactionCategories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "TransactionRules",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                MatchText = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                MatchType = table.Column<int>(type: "INTEGER", nullable: false),
                AccountId = table.Column<int>(type: "INTEGER", nullable: true),
                TransactionType = table.Column<int>(type: "INTEGER", nullable: true),
                TransactionCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                Priority = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransactionRules", x => x.Id);
                table.ForeignKey("FK_TransactionRules_Accounts_AccountId", x => x.AccountId, "Accounts", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_TransactionRules_TransactionCategories_TransactionCategoryId",
                    x => x.TransactionCategoryId, "TransactionCategories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<int>(name: "CategoryId", table: "Transactions", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<int>(name: "CategoryId", table: "StatementImportRows", type: "INTEGER", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "CategorySuggestedByRule", table: "StatementImportRows", type: "INTEGER", nullable: false, defaultValue: false);

        migrationBuilder.CreateIndex(name: "IX_TransactionCategories_ParentCategoryId", table: "TransactionCategories", column: "ParentCategoryId");
        migrationBuilder.CreateIndex(name: "IX_TransactionCategories_Type_ParentCategoryId_Name", table: "TransactionCategories", columns: new[] { "Type", "ParentCategoryId", "Name" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_TransactionCategories_Type_IsActive_DisplayOrder", table: "TransactionCategories", columns: new[] { "Type", "IsActive", "DisplayOrder" });
        migrationBuilder.CreateIndex(name: "IX_TransactionCategoryMappings_TransactionCategoryId", table: "TransactionCategoryMappings", column: "TransactionCategoryId");
        migrationBuilder.CreateIndex(name: "IX_TransactionCategoryMappings_Profile_NormalizedSourceCategory", table: "TransactionCategoryMappings", columns: new[] { "Profile", "NormalizedSourceCategory" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_TransactionRules_AccountId", table: "TransactionRules", column: "AccountId");
        migrationBuilder.CreateIndex(name: "IX_TransactionRules_TransactionCategoryId", table: "TransactionRules", column: "TransactionCategoryId");
        migrationBuilder.CreateIndex(name: "IX_TransactionRules_IsActive_Priority", table: "TransactionRules", columns: new[] { "IsActive", "Priority" });
        migrationBuilder.CreateIndex(name: "IX_Transactions_CategoryId", table: "Transactions", column: "CategoryId");
        migrationBuilder.CreateIndex(name: "IX_StatementImportRows_CategoryId", table: "StatementImportRows", column: "CategoryId");

        migrationBuilder.AddForeignKey(name: "FK_Transactions_TransactionCategories_CategoryId", table: "Transactions",
            column: "CategoryId", principalTable: "TransactionCategories", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_StatementImportRows_TransactionCategories_CategoryId", table: "StatementImportRows",
            column: "CategoryId", principalTable: "TransactionCategories", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Transactions_TransactionCategories_CategoryId", table: "Transactions");
        migrationBuilder.DropForeignKey(name: "FK_StatementImportRows_TransactionCategories_CategoryId", table: "StatementImportRows");
        migrationBuilder.DropIndex(name: "IX_Transactions_CategoryId", table: "Transactions");
        migrationBuilder.DropIndex(name: "IX_StatementImportRows_CategoryId", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "CategoryId", table: "Transactions");
        migrationBuilder.DropColumn(name: "CategoryId", table: "StatementImportRows");
        migrationBuilder.DropColumn(name: "CategorySuggestedByRule", table: "StatementImportRows");
        migrationBuilder.DropTable(name: "TransactionRules");
        migrationBuilder.DropTable(name: "TransactionCategoryMappings");
        migrationBuilder.DropTable(name: "TransactionCategories");
    }
}
