using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// Every query is limited to the signed-in user's budgets (see ExpenseService).
public class BudgetService(PennyWiseDbContext db, AppClock clock, CurrentUser currentUser)
{
    // Either every budget starting in a month (any period), or the one budget for a window.
    public async Task<List<BudgetResponse>> ListAsync(string? month, BudgetWindow? window = null)
    {
        var userId = currentUser.Id;
        var query = db.Budgets.AsNoTracking().Include(b => b.CategoryLimits).Where(b => b.UserId == userId);
        if (month is not null) query = query.Where(b => b.Month == month);
        if (window is { } w) query = query.Where(b => b.Period == w.Period && b.StartDate == w.Start);

        var budgets = await query.OrderByDescending(b => b.StartDate).ThenBy(b => b.Period).ToListAsync();
        return budgets.Select(BudgetResponse.From).ToList();
    }

    public async Task<BudgetResponse> GetAsync(int id) =>
        BudgetResponse.From(await FindAsync(id));

    public async Task<BudgetResponse> CreateAsync(BudgetRequest request)
    {
        var window = request.ToWindow();
        await EnsurePeriodIsFreeAsync(window, exceptBudgetId: null);

        var budget = new Budget { UserId = currentUser.Id, CreatedAt = clock.UtcNow };
        SetWindow(budget, window);
        Apply(budget, request);
        db.Budgets.Add(budget);
        await SaveAsync(window);
        return BudgetResponse.From(budget);
    }

    public async Task<BudgetResponse> UpdateAsync(int id, BudgetRequest request)
    {
        var budget = await FindAsync(id);
        var window = request.ToWindow();
        await EnsurePeriodIsFreeAsync(window, exceptBudgetId: id);

        SetWindow(budget, window);
        Apply(budget, request);
        await SaveAsync(window);
        return BudgetResponse.From(budget);
    }

    // Removes the budget (and its category limits). Expenses are not touched;
    // the period simply has no budget again.
    public async Task DeleteAsync(int id)
    {
        db.Budgets.Remove(await FindAsync(id));
        await db.SaveChangesAsync();
    }

    private async Task<Budget> FindAsync(int id)
    {
        var userId = currentUser.Id;
        return await db.Budgets.Include(b => b.CategoryLimits).FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId)
            ?? throw new NotFoundException($"Budget {id} was not found.");
    }

    private static void SetWindow(Budget budget, BudgetWindow window)
    {
        budget.Period = window.Period;
        budget.StartDate = window.Start;
        budget.Month = window.Month;
    }

    private async Task EnsurePeriodIsFreeAsync(BudgetWindow window, int? exceptBudgetId)
    {
        var userId = currentUser.Id;
        if (await db.Budgets.AnyAsync(b => b.UserId == userId && b.Period == window.Period && b.StartDate == window.Start && b.Id != exceptBudgetId))
        {
            throw new ConflictException(AlreadyExists(window));
        }
    }

    private static string AlreadyExists(BudgetWindow window) =>
        $"A budget for {window.Describe()} already exists. Edit that budget instead.";

    // Updates limits in place: changes existing ones, removes the ones no longer
    // sent, and adds new ones. Replacing them all at once would briefly break the
    // "one limit per category" unique index.
    private void Apply(Budget budget, BudgetRequest request)
    {
        budget.Amount = request.Amount!.Value;
        budget.UpdatedAt = clock.UtcNow;

        var requested = request.CategoryLimits.ToDictionary(
            l => Enum.Parse<Category>(l.Category!), l => l.Amount!.Value);

        budget.CategoryLimits.RemoveAll(l => !requested.ContainsKey(l.Category));
        foreach (var (category, amount) in requested)
        {
            var existing = budget.CategoryLimits.FirstOrDefault(l => l.Category == category);
            if (existing is not null) existing.Amount = amount;
            else budget.CategoryLimits.Add(new CategoryLimit { Category = category, Amount = amount });
        }
    }

    // The unique index on (period, start date) is the final safety net if two requests race.
    private async Task SaveAsync(BudgetWindow window)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            throw new ConflictException(AlreadyExists(window));
        }
    }
}
