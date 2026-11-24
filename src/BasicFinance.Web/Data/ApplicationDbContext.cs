using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<FinancialTransaction> Transactions => Set<FinancialTransaction>();

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

        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.Property(transaction => transaction.Amount).HasColumnType("TEXT");
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
