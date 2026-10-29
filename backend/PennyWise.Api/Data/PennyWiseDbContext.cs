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
            entity.Property(u => u.AllowancePeriod).HasConversion<string>().HasMaxLength(10)
                .HasDefaultValue(AllowancePeriod.Monthly).HasSentinel((AllowancePeriod)(-1));
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
            // One budget per month per user; different users can budget the same month.
            entity.HasIndex(b => new { b.UserId, b.Month }).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(b => b.CategoryLimits)
                .WithOne()
                .HasForeignKey(l => l.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);
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
