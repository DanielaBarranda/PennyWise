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
public class DashboardService(PennyWiseDbContext db, CurrentUser currentUser, CarryOverService carryOvers)
{
    // Must match WARNING_PERCENT in frontend/src/utils/constants.js.
    public const decimal WarningPercent = 80m;
    private const int RecentCount = 5;

    // One period only: its own budget and the expenses dated inside it.
    public async Task<DashboardSummaryResponse> GetSummaryAsync(BudgetWindow window)
    {
        var (first, last) = (window.Start, window.End);
        var userId = currentUser.Id;

        var budget = await db.Budgets.AsNoTracking()
            .Include(b => b.CategoryLimits)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == window.Period && b.StartDate == window.Start);

        // SQLite cannot SUM decimal columns exactly, so the period's expenses are
        // loaded and added up in C# with decimal arithmetic. One person's month
        // of expenses is small, so this is fast.
        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= first && e.ExpenseDate <= last)
            .ToListAsync();

        var totalSpent = expenses.Sum(e => e.Amount);
        // Money moved into savings in the period also comes out of its budget.
        var saved = await SavingsService.SetAsideAsync(db, userId, first, last);
        // Money left here and added to the next period's budget counts as used.
        var carriedOut = await CarryOverService.CarriedOutAsync(db, budget?.Id);
        var used = totalSpent + saved + carriedOut;

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

        // Savings moves are listed beside expenses, since they also use the budget.
        var recentSavings = await db.SavingsTransactions.AsNoTracking()
            .Where(t => t.Date >= first && t.Date <= last && db.SavingsGoals.Any(g => g.Id == t.GoalId && g.UserId == userId))
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Take(RecentCount)
            .Select(t => new RecentSavingsMove(
                t.Id, t.GoalId,
                db.SavingsGoals.Where(g => g.Id == t.GoalId).Select(g => g.Name).First(),
                t.Type.ToString(), t.Amount, t.Date, t.Note, t.CreatedAt))
            .ToListAsync();

        return new DashboardSummaryResponse(
            Period: window.Period.ToString(),
            Start: window.Start,
            End: window.End,
            Month: window.Month,
            Budget: budget is null ? null : BudgetResponse.From(budget),
            TotalSpent: totalSpent,
            Saved: saved,
            CarriedIn: await CarryOverService.CarriedInAsync(db, budget?.Id),
            CarriedOut: carriedOut,
            Remaining: budget is null ? null : budget.Amount - used,
            PercentSpent: budget is null ? null : Math.Round(used / budget.Amount * 100, 2),
            Status: budget is null ? "NoBudget" : StatusFor(used, budget.Amount),
            ExpenseCount: expenses.Count,
            Categories: categories,
            RecentExpenses: recent,
            RecentSavings: recentSavings,
            LeftoverOffer: await carryOvers.OfferAsync(window, budget?.Id));
    }

    // Below 80% = OnTrack, 80% up to 100% = Warning, above 100% = Exceeded.
    private static string StatusFor(decimal spent, decimal limit)
    {
        if (spent > limit) return "Exceeded";
        if (spent * 100 >= limit * WarningPercent) return "Warning";
        return "OnTrack";
    }
}
