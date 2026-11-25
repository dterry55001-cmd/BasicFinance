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
    public DbSet<DebtInterestRateHistory> DebtInterestRateHistory => Set<DebtInterestRateHistory>();
    public DbSet<CreditLimitHistory> CreditLimitHistory => Set<CreditLimitHistory>();
    public DbSet<ReportedBalanceSnapshot> ReportedBalanceSnapshots => Set<ReportedBalanceSnapshot>();
    public DbSet<CreditInquiry> CreditInquiries => Set<CreditInquiry>();
    public DbSet<DebtFreeTarget> DebtFreeTargets => Set<DebtFreeTarget>();
    public DbSet<MonthlyDebtReductionGoal> MonthlyDebtReductionGoals => Set<MonthlyDebtReductionGoal>();
    public DbSet<DebtFreeMilestone> DebtFreeMilestones => Set<DebtFreeMilestone>();
    public DbSet<DebtNotification> DebtNotifications => Set<DebtNotification>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<BudgetAlertPreference> BudgetAlertPreferences => Set<BudgetAlertPreference>();
    public DbSet<BudgetAlert> BudgetAlerts => Set<BudgetAlert>();
    public DbSet<RecurringCashFlow> RecurringCashFlows => Set<RecurringCashFlow>();

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

        modelBuilder.Entity<DebtInterestRateHistory>(entity =>
        {
            entity.Property(x => x.AnnualRatePercent).HasColumnType("TEXT");
            entity.Property(x => x.RateType).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.EffectiveDate });
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DebtToIncomeRecord>(entity =>
        {
            entity.Property(record => record.GrossMonthlyIncome).HasColumnType("TEXT");
            entity.Property(record => record.HousingMonthlyPayment).HasColumnType("TEXT");
            entity.Property(record => record.OtherMonthlyDebtPayment).HasColumnType("TEXT");
            entity.Property(record => record.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(record => new { record.Month, record.CurrencyCode }).IsUnique();
        });

        modelBuilder.Entity<CreditLimitHistory>(entity =>
        {
            entity.Property(x => x.PreviousLimit).HasColumnType("TEXT");
            entity.Property(x => x.NewLimit).HasColumnType("TEXT");
            entity.Property(x => x.Source).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.EffectiveDate });
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReportedBalanceSnapshot>(entity =>
        {
            entity.Property(x => x.ReportedBalance).HasColumnType("TEXT");
            entity.Property(x => x.ReportedCreditLimit).HasColumnType("TEXT");
            entity.Property(x => x.Source).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.ReportedDate, x.Source }).IsUnique();
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditInquiry>(entity =>
        {
            entity.Property(x => x.Bureau).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Lender).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.InquiryDate);
            entity.HasIndex(x => x.Bureau);
        });

        modelBuilder.Entity<DebtFreeTarget>(entity =>
        {
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.ExtraMonthlyPayment).HasColumnType("TEXT");
            entity.HasIndex(x => x.CurrencyCode).IsUnique();
        });

        modelBuilder.Entity<MonthlyDebtReductionGoal>(entity =>
        {
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(x => x.TargetReduction).HasColumnType("TEXT");
            entity.HasIndex(x => new { x.CurrencyCode, x.Month }).IsUnique();
        });

        modelBuilder.Entity<DebtFreeMilestone>(entity =>
        {
            entity.HasIndex(x => new { x.AccountId, x.AchievedDate });
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DebtNotification>(entity =>
        {
            entity.Property(x => x.NotificationKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Level).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.NotificationKey).IsUnique();
            entity.HasIndex(x => new { x.IsRead, x.CreatedUtc });
        });

        modelBuilder.Entity<BudgetPeriod>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => new { x.Year, x.Month, x.CurrencyCode }).IsUnique();
            entity.HasMany(x => x.Lines).WithOne(x => x.BudgetPeriod).HasForeignKey(x => x.BudgetPeriodId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BudgetAlertPreference>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<BudgetAlert>(entity =>
        {
            entity.Property(x => x.Level).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ThresholdPercent).HasColumnType("INTEGER");
            entity.Property(x => x.ActualAmount).HasColumnType("TEXT");
            entity.HasIndex(x => new { x.BudgetPeriodId, x.CategoryId, x.ThresholdPercent }).IsUnique();
            entity.HasIndex(x => new { x.IsRead, x.CreatedUtc });
            entity.HasOne(x => x.BudgetPeriod).WithMany().HasForeignKey(x => x.BudgetPeriodId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecurringCashFlow>(entity =>
        {
            entity.Property(x => x.Amount).HasColumnType("TEXT");
            entity.Property(x => x.Description).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.HasIndex(x => new { x.IsActive, x.NextDueDate });
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetLine>(entity =>
        {
            entity.Property(x => x.PlannedAmount).HasColumnType("TEXT");
            entity.Property(x => x.Notes).HasMaxLength(300);
            entity.HasIndex(x => new { x.BudgetPeriodId, x.CategoryId }).IsUnique();
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
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
