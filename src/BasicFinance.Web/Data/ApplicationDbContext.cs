using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FinancialTransaction> Transactions => Set<FinancialTransaction>();
    public DbSet<StatementImport> StatementImports => Set<StatementImport>();
    public DbSet<StatementImportRow> StatementImportRows => Set<StatementImportRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(account => account.Name).HasMaxLength(100).IsRequired();
            entity.Property(account => account.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(account => account.OpeningBalance).HasColumnType("TEXT");
            entity.HasIndex(account => account.Status);
        });

        modelBuilder.Entity<StatementImport>(entity =>
        {
            entity.Property(batch => batch.FileName).HasMaxLength(255).IsRequired();
            entity.HasOne(batch => batch.Account).WithMany().HasForeignKey(batch => batch.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(batch => batch.Rows).WithOne(row => row.StatementImport).HasForeignKey(row => row.StatementImportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(batch => batch.CreatedUtc);
        });

        modelBuilder.Entity<StatementImportRow>(entity =>
        {
            entity.Property(row => row.Amount).HasColumnType("TEXT");
            entity.Property(row => row.UnitCount).HasColumnType("TEXT");
            entity.Property(row => row.UnitValue).HasColumnType("TEXT");
            entity.Property(row => row.Quantity).HasColumnType("TEXT");
            entity.Property(row => row.Price).HasColumnType("TEXT");
            entity.Property(row => row.Description).HasMaxLength(160).IsRequired();
            entity.HasIndex(row => new { row.StatementImportId, row.RowNumber }).IsUnique();
        });

        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.Property(transaction => transaction.Amount).HasColumnType("TEXT");
            entity.Property(transaction => transaction.UnitCount).HasColumnType("TEXT");
            entity.Property(transaction => transaction.UnitValue).HasColumnType("TEXT");
            entity.Property(transaction => transaction.Quantity).HasColumnType("TEXT");
            entity.Property(transaction => transaction.Price).HasColumnType("TEXT");
            entity.Property(transaction => transaction.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(transaction => transaction.TransactionDate);
            entity.HasIndex(transaction => transaction.AccountId);
            entity.HasIndex(transaction => transaction.RelatedAccountId);
            entity.HasOne(transaction => transaction.Account)
                .WithMany()
                .HasForeignKey(transaction => transaction.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.RelatedAccount)
                .WithMany()
                .HasForeignKey(transaction => transaction.RelatedAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
