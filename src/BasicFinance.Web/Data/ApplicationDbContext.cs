using BasicFinance.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BasicFinance.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

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
    }
}
