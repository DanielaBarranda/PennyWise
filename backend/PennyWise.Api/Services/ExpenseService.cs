using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Services;

// Business logic for expenses. Controllers handle HTTP; this class handles data.
public class ExpenseService(PennyWiseDbContext db, AppClock clock)
{
    public async Task<List<ExpenseResponse>> ListAsync(string? month, Category? category)
    {
        var query = db.Expenses.AsNoTracking();

        if (month is not null)
        {
            var (first, last) = MonthKey.Range(month);
            query = query.Where(e => e.ExpenseDate >= first && e.ExpenseDate <= last);
        }
        if (category is not null)
        {
            query = query.Where(e => e.Category == category);
        }

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        return expenses.Select(ExpenseResponse.From).ToList();
    }

    public async Task<ExpenseResponse> GetAsync(int id) =>
        ExpenseResponse.From(await FindAsync(id));

    public async Task<ExpenseResponse> CreateAsync(ExpenseRequest request)
    {
        var expense = new Expense { CreatedAt = clock.UtcNow };
        Apply(expense, request);
        db.Expenses.Add(expense);
        await db.SaveChangesAsync();
        return ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse> UpdateAsync(int id, ExpenseRequest request)
    {
        var expense = await FindAsync(id);
        Apply(expense, request);
        await db.SaveChangesAsync();
        return ExpenseResponse.From(expense);
    }

    public async Task DeleteAsync(int id)
    {
        var expense = await FindAsync(id);
        db.Expenses.Remove(expense);
        await db.SaveChangesAsync();
    }

    private async Task<Expense> FindAsync(int id) =>
        await db.Expenses.FindAsync(id) ?? throw new NotFoundException($"Expense {id} was not found.");

    // The request was already validated, so the ! and Parse calls cannot fail here.
    private void Apply(Expense expense, ExpenseRequest request)
    {
        expense.Amount = request.Amount!.Value;
        expense.Category = Enum.Parse<Category>(request.Category!);
        expense.Description = request.Description!.Trim();
        expense.ExpenseDate = request.ExpenseDate!.Value;
        expense.UpdatedAt = clock.UtcNow;
    }
}
