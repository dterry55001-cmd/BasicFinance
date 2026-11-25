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
    public DbSet<TransactionCategory> TransactionCategories => Set<TransactionCategory>();
    public DbSet<TransactionCategoryMapping> TransactionCategoryMappings => Set<TransactionCategoryMapping>();
    public DbSet<TransactionRule> TransactionRules => Set<TransactionRule>();
    public DbSet<AccountBalanceSnapshot> AccountBalanceSnapshots => Set<AccountBalanceSnapshot>();
    public DbSet<InterestRecord> InterestRecords => Set<InterestRecord>();
    public DbSet<DebtToIncomeRecord> DebtToIncomeRecords => Set<DebtToIncomeRecord>();
    public DbSet<DebtPaymentRecord> DebtPaymentRecords => Set<DebtPaymentRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(account => account.Name).HasMaxLength(100).IsRequired();
            entity.Property(account => account.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(account => account.OpeningBalance).HasColumnType("TEXT");
            entity.Property(account => account.InterestRatePercent).HasColumnType("TEXT");
            entity.Property(account => account.CreditLimit).HasColumnType("TEXT");
            entity.Property(account => account.MinimumPayment).HasColumnType("TEXT");
            entity.Property(account => account.OriginalLoanAmount).HasColumnType("TEXT");
            entity.Property(account => account.EmployerMatchPercent).HasColumnType("TEXT");
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
            entity.Property(row => row.SourceSignedAmount).HasColumnType("TEXT");
            entity.Property(row => row.UnitCount).HasColumnType("TEXT");
            entity.Property(row => row.UnitValue).HasColumnType("TEXT");
            entity.Property(row => row.Quantity).HasColumnType("TEXT");
            entity.Property(row => row.Price).HasColumnType("TEXT");
            entity.Property(row => row.RunningBalance).HasColumnType("TEXT");
            entity.Property(row => row.Description).HasMaxLength(160).IsRequired();
            entity.Property(row => row.SourceFieldsJson).HasColumnType("TEXT").IsRequired();
            entity.HasIndex(row => row.DuplicateFingerprint);
            entity.HasIndex(row => row.CategoryId);
            entity.HasOne(row => row.TransactionCategory)
                .WithMany()
                .HasForeignKey(row => row.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(row => new { row.StatementImportId, row.RowNumber }).IsUnique();
        });

        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.Property(transaction => transaction.Amount).HasColumnType("TEXT");
            entity.Property(transaction => transaction.UnitCount).HasColumnType("TEXT");
            entity.Property(transaction => transaction.UnitValue).HasColumnType("TEXT");
            entity.Property(transaction => transaction.Quantity).HasColumnType("TEXT");
            entity.Property(transaction => transaction.Price).HasColumnType("TEXT");
            entity.Property(transaction => transaction.SourceSignedAmount).HasColumnType("TEXT");
            entity.Property(transaction => transaction.RunningBalance).HasColumnType("TEXT");
            entity.Property(transaction => transaction.SourceFieldsJson).HasColumnType("TEXT").IsRequired();
            entity.HasIndex(transaction => transaction.ImportFingerprint).IsUnique().HasFilter("\"ImportFingerprint\" IS NOT NULL");
            entity.Property(transaction => transaction.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(transaction => transaction.TransactionDate);
            entity.HasIndex(transaction => transaction.AccountId);
            entity.HasIndex(transaction => transaction.RelatedAccountId);
            entity.HasIndex(transaction => transaction.CategoryId);
            entity.HasOne(transaction => transaction.Account)
                .WithMany()
                .HasForeignKey(transaction => transaction.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.RelatedAccount)
                .WithMany()
                .HasForeignKey(transaction => transaction.RelatedAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(transaction => transaction.TransactionCategory)
                .WithMany()
                .HasForeignKey(transaction => transaction.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TransactionCategory>(entity =>
        {
            entity.Property(category => category.Name).HasMaxLength(80).IsRequired();
            entity.Property(category => category.Description).HasMaxLength(300);
            entity.HasIndex(category => new { category.Type, category.ParentCategoryId, category.Name }).IsUnique();
            entity.HasIndex(category => new { category.Type, category.IsActive, category.DisplayOrder });
            entity.HasOne(category => category.ParentCategory)
                .WithMany(category => category.Children)
                .HasForeignKey(category => category.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TransactionCategoryMapping>(entity =>
        {
            entity.Property(mapping => mapping.SourceCategory).HasMaxLength(100).IsRequired();
            entity.Property(mapping => mapping.NormalizedSourceCategory).HasMaxLength(100).IsRequired();
            entity.HasIndex(mapping => new { mapping.Profile, mapping.NormalizedSourceCategory }).IsUnique();
            entity.HasOne(mapping => mapping.TransactionCategory)
                .WithMany()
                .HasForeignKey(mapping => mapping.TransactionCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TransactionRule>(entity =>
        {
            entity.Property(rule => rule.Name).HasMaxLength(100).IsRequired();
            entity.Property(rule => rule.MatchText).HasMaxLength(160).IsRequired();
            entity.HasIndex(rule => new { rule.IsActive, rule.Priority });
            entity.HasOne(rule => rule.Account)
                .WithMany()
                .HasForeignKey(rule => rule.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(rule => rule.TransactionCategory)
                .WithMany()
                .HasForeignKey(rule => rule.TransactionCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccountBalanceSnapshot>(entity =>
        {
            entity.Property(snapshot => snapshot.Balance).HasColumnType("TEXT");
            entity.HasIndex(snapshot => new { snapshot.AccountId, snapshot.SnapshotDate }).IsUnique();
            entity.HasOne(snapshot => snapshot.Account).WithMany().HasForeignKey(snapshot => snapshot.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DebtPaymentRecord>(entity =>
        {
            entity.Property(payment => payment.ExpectedAmount).HasColumnType("TEXT");
            entity.Property(payment => payment.ActualAmount).HasColumnType("TEXT");
            entity.Property(payment => payment.PrincipalPaid).HasColumnType("TEXT");
            entity.Property(payment => payment.InterestPaid).HasColumnType("TEXT");
            entity.Property(payment => payment.FeesPaid).HasColumnType("TEXT");
            entity.HasIndex(payment => new { payment.AccountId, payment.DueDate }).IsUnique();
            entity.HasIndex(payment => new { payment.Status, payment.DueDate });
            entity.HasIndex(payment => payment.TransactionId).IsUnique();
            entity.HasOne(payment => payment.Account).WithMany().HasForeignKey(payment => payment.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.SourceAccount).WithMany().HasForeignKey(payment => payment.SourceAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(payment => payment.Transaction).WithMany().HasForeignKey(payment => payment.TransactionId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DebtToIncomeRecord>(entity =>
        {
            entity.Property(record => record.GrossMonthlyIncome).HasColumnType("TEXT");
            entity.Property(record => record.HousingMonthlyPayment).HasColumnType("TEXT");
            entity.Property(record => record.OtherMonthlyDebtPayment).HasColumnType("TEXT");
            entity.Property(record => record.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(record => new { record.Month, record.CurrencyCode }).IsUnique();
        });

        modelBuilder.Entity<InterestRecord>(entity =>
        {
            entity.Property(record => record.Amount).HasColumnType("TEXT");
            entity.Property(record => record.Description).HasMaxLength(160).IsRequired();
            entity.Property(record => record.Notes).HasMaxLength(500);
            entity.HasIndex(record => new { record.AccountId, record.InterestDate });
            entity.HasOne(record => record.Account).WithMany().HasForeignKey(record => record.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(record => record.Transaction).WithMany().HasForeignKey(record => record.TransactionId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
