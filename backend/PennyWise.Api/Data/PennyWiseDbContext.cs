using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Models;

namespace PennyWise.Api.Data;

// Entity Framework Core turns these classes into SQLite tables and builds
// parameterized SQL for every query, which prevents SQL injection.
public class PennyWiseDbContext(DbContextOptions<PennyWiseDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();
    public DbSet<SavingsGoal> SavingsGoals => Set<SavingsGoal>();
    public DbSet<SavingsTransaction> SavingsTransactions => Set<SavingsTransaction>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<BudgetCarryOver> BudgetCarryOvers => Set<BudgetCarryOver>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Name).HasMaxLength(80).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(254).IsRequired();
            entity.Property(u => u.NormalizedEmail).HasMaxLength(254).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
            entity.HasIndex(u => u.NormalizedEmail).IsUnique();
            entity.Property(u => u.GoogleSubject).HasMaxLength(255);
            entity.HasIndex(u => u.GoogleSubject).IsUnique();
            entity.Property(u => u.TwoFactorSecret).HasMaxLength(500);
            entity.Property(u => u.TwoFactorPendingSecret).HasMaxLength(500);
            entity.Property(u => u.RecoveryCodeHashes).HasMaxLength(1000);
            entity.Property(u => u.BudgetPeriod).HasColumnName("AllowancePeriod").HasConversion<string>().HasMaxLength(10)
                .HasDefaultValue(BudgetPeriod.Monthly).HasSentinel((BudgetPeriod)(-1));
        });

        modelBuilder.Entity<UserAvatar>(entity =>
        {
            entity.HasKey(a => a.UserId);
            entity.Property(a => a.ContentType).HasMaxLength(20).IsRequired();
            entity.Property(a => a.Data).IsRequired();
            entity.HasOne<User>().WithOne().HasForeignKey<UserAvatar>(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SavingsGoal>(entity =>
        {
            entity.Property(g => g.Name).HasMaxLength(80).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(200);
            entity.HasIndex(g => g.UserId);
            entity.HasOne<User>().WithMany().HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(g => g.Transactions).WithOne().HasForeignKey(t => t.GoalId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SavingsTransaction>(entity =>
        {
            entity.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Note).HasMaxLength(120);
            entity.HasIndex(t => new { t.GoalId, t.Date });
            entity.HasIndex(t => t.ExpenseId).IsUnique();
            entity.HasOne<Expense>().WithMany().HasForeignKey(t => t.ExpenseId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Bill>(entity =>
        {
            entity.Property(b => b.Name).HasMaxLength(80).IsRequired();
            entity.Property(b => b.Notes).HasMaxLength(200);
            entity.Property(b => b.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(b => b.Frequency).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(b => b.UserId);
            entity.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(b => b.Payments).WithOne().HasForeignKey(p => p.BillId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BillPayment>(entity =>
        {
            // One payment per bill occurrence: paying the same due date twice is impossible.
            entity.HasIndex(p => new { p.BillId, p.DueDate }).IsUnique();
            // Deleting the linked expense keeps the payment record (it just loses the link).
            entity.HasOne(p => p.Expense).WithMany().HasForeignKey(p => p.ExpenseId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(200).IsRequired();
            // Every query filters by owner and month, so index both together.
            entity.HasIndex(e => new { e.UserId, e.ExpenseDate });
            entity.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Budget>(entity =>
        {
            entity.Property(b => b.Month).HasMaxLength(7).IsRequired();
            entity.Property(b => b.Period).HasConversion<string>().HasMaxLength(10)
                .HasDefaultValue(BudgetPeriod.Monthly).HasSentinel((BudgetPeriod)(-1));
            // One budget per period per user (e.g. one for the week of Nov 2); different
            // users can budget the same period.
            entity.HasIndex(b => new { b.UserId, b.Period, b.StartDate }).IsUnique();
            entity.HasIndex(b => new { b.UserId, b.Month });
            entity.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(b => b.CategoryLimits)
                .WithOne()
                .HasForeignKey(l => l.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BudgetCarryOver>(entity =>
        {
            entity.Property(c => c.Action).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.Category).HasConversion<string>().HasMaxLength(20);
            // One decision per finished budget.
            entity.HasIndex(c => c.FromBudgetId).IsUnique();
            entity.HasIndex(c => c.ToBudgetId);
            entity.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            // Removing either budget removes the decision, so the leftover can be decided again.
            entity.HasOne<Budget>().WithMany().HasForeignKey(c => c.FromBudgetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Budget>().WithMany().HasForeignKey(c => c.ToBudgetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SavingsTransaction>().WithMany().HasForeignKey(c => c.SavingsTransactionId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CategoryLimit>(entity =>
        {
            entity.ToTable("CategoryLimits");
            entity.Property(l => l.Category).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(l => new { l.BudgetId, l.Category }).IsUnique();
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite stores dates as text and forgets that they were UTC.
        // This converter marks them as UTC again when they are read back.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter()
        : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
