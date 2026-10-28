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
