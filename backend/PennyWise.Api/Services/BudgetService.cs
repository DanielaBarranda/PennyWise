using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

public class BudgetService(PennyWiseDbContext db, AppClock clock)
{
    public async Task<List<BudgetResponse>> ListAsync(string? month)
    {
        var query = db.Budgets.AsNoTracking().Include(b => b.CategoryLimits).AsQueryable();
        if (month is not null) query = query.Where(b => b.Month == month);

        var budgets = await query.OrderByDescending(b => b.Month).ToListAsync();
        return budgets.Select(BudgetResponse.From).ToList();
    }

    public async Task<BudgetResponse> GetAsync(int id) =>
        BudgetResponse.From(await FindAsync(id));

    public async Task<BudgetResponse> CreateAsync(BudgetRequest request)
    {
        await EnsureMonthIsFreeAsync(request.Month!, exceptBudgetId: null);

        var budget = new Budget { Month = request.Month!, CreatedAt = clock.UtcNow };
        Apply(budget, request);
        db.Budgets.Add(budget);
        await SaveAsync(budget.Month);
        return BudgetResponse.From(budget);
    }

    public async Task<BudgetResponse> UpdateAsync(int id, BudgetRequest request)
    {
        var budget = await FindAsync(id);
        await EnsureMonthIsFreeAsync(request.Month!, exceptBudgetId: id);

        budget.Month = request.Month!;
        Apply(budget, request);
        await SaveAsync(budget.Month);
        return BudgetResponse.From(budget);
    }

    private async Task<Budget> FindAsync(int id) =>
        await db.Budgets.Include(b => b.CategoryLimits).FirstOrDefaultAsync(b => b.Id == id)
        ?? throw new NotFoundException($"Budget {id} was not found.");

    private async Task EnsureMonthIsFreeAsync(string month, int? exceptBudgetId)
    {
        if (await db.Budgets.AnyAsync(b => b.Month == month && b.Id != exceptBudgetId))
        {
            throw new ConflictException($"A budget for {month} already exists. Edit that budget instead.");
        }
    }

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

    // The unique index on Month is the final safety net if two requests race.
    private async Task SaveAsync(string month)
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            throw new ConflictException($"A budget for {month} already exists. Edit that budget instead.");
        }
    }
}
