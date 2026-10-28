using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Services;

// Calculates every dashboard number on the server from the database, so the
// frontend only displays values; it never invents totals of its own.
// Only the signed-in user's budget and expenses are counted.
public class DashboardService(PennyWiseDbContext db, CurrentUser currentUser)
{
    // Must match WARNING_PERCENT in frontend/src/utils/constants.js.
    public const decimal WarningPercent = 80m;
    private const int RecentCount = 5;

    public async Task<DashboardSummaryResponse> GetSummaryAsync(string month)
    {
        var (first, last) = MonthKey.Range(month);
        var userId = currentUser.Id;

        var budget = await db.Budgets.AsNoTracking()
            .Include(b => b.CategoryLimits)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Month == month);

        // SQLite cannot SUM decimal columns exactly, so the month's expenses are
        // loaded and added up in C# with decimal arithmetic. One person's month
        // of expenses is small, so this is fast.
        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= first && e.ExpenseDate <= last)
            .ToListAsync();

        var totalSpent = expenses.Sum(e => e.Amount);

        var categories = Enum.GetValues<Category>().Select(category =>
        {
            var total = expenses.Where(e => e.Category == category).Sum(e => e.Amount);
            var limit = budget?.CategoryLimits.FirstOrDefault(l => l.Category == category)?.Amount;
            return new CategorySummary(category.ToString(), total, limit, limit is null ? "NoLimit" : StatusFor(total, limit.Value));
        }).ToList();

        var recent = expenses
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(RecentCount)
            .Select(ExpenseResponse.From)
            .ToList();

        return new DashboardSummaryResponse(
            Month: month,
            Budget: budget is null ? null : BudgetResponse.From(budget),
            TotalSpent: totalSpent,
            Remaining: budget is null ? null : budget.Amount - totalSpent,
            PercentSpent: budget is null ? null : Math.Round(totalSpent / budget.Amount * 100, 2),
            Status: budget is null ? "NoBudget" : StatusFor(totalSpent, budget.Amount),
            ExpenseCount: expenses.Count,
            Categories: categories,
            RecentExpenses: recent);
    }

    // Below 80% = OnTrack, 80% up to 100% = Warning, above 100% = Exceeded.
    private static string StatusFor(decimal spent, decimal limit)
    {
        if (spent > limit) return "Exceeded";
        if (spent * 100 >= limit * WarningPercent) return "Warning";
        return "OnTrack";
    }
}
