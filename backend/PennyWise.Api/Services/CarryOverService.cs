using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// Money left at the end of a budget period. Once the period is over, the user is
// asked (once) what to do with it: add it to the next period's budget (optionally
// to one category's limit), move it into a savings goal, or leave it.
//
//   left = budget - spent - moved into savings - already added to the next period
//
// Only the period right before the receiving one can be carried, and only once.
public class CarryOverService(PennyWiseDbContext db, AppClock clock, CurrentUser currentUser)
{
    // Money from this budget that was added to the next period's budget.
    public static async Task<decimal> CarriedOutAsync(PennyWiseDbContext db, int? budgetId) =>
        budgetId is null ? 0 : (await db.BudgetCarryOvers.AsNoTracking()
            .Where(c => c.FromBudgetId == budgetId && c.Action == CarryOverAction.AddToBudget)
            .Select(c => c.Amount).ToListAsync()).Sum();

    // Money added to this budget from the period before it (already part of its Amount).
    public static async Task<decimal> CarriedInAsync(PennyWiseDbContext db, int? budgetId) =>
        budgetId is null ? 0 : (await db.BudgetCarryOvers.AsNoTracking()
            .Where(c => c.ToBudgetId == budgetId && c.Action == CarryOverAction.AddToBudget)
            .Select(c => c.Amount).ToListAsync()).Sum();

    // What was left in a budget's own period (negative when over).
    public static async Task<decimal> LeftAsync(PennyWiseDbContext db, int userId, Budget budget)
    {
        var window = BudgetWindow.For(budget.Period, budget.StartDate);
        var spent = (await SavingsService.BudgetExpenses(db, userId)
            .Where(e => e.ExpenseDate >= window.Start && e.ExpenseDate <= window.End)
            .Select(e => e.Amount).ToListAsync()).Sum();
        var saved = await SavingsService.SetAsideAsync(db, userId, window.Start, window.End);
        return budget.Amount - spent - saved - await CarriedOutAsync(db, budget.Id);
    }

    // The leftover the user can still decide about for `window` (whose budget is
    // `budgetId`): the previous period must be over, have a budget with money
    // left, and no decision yet. Null otherwise.
    public async Task<LeftoverOffer?> OfferAsync(BudgetWindow window, int? budgetId)
    {
        if (budgetId is null) return null;
        if (await FindPreviousAsync(window) is not { } prev) return null;
        return new LeftoverOffer(prev.Budget.Id, window.Period.ToString(), prev.Window.Start, prev.Window.End, prev.Left);
    }

    public async Task<CarryOverResponse> ApplyAsync(int toBudgetId, CarryOverRequest request)
    {
        var userId = currentUser.Id;
        var to = await db.Budgets.Include(b => b.CategoryLimits).FirstOrDefaultAsync(b => b.Id == toBudgetId && b.UserId == userId)
            ?? throw new NotFoundException($"Budget {toBudgetId} was not found.");
        var window = BudgetWindow.For(to.Period, to.StartDate);
        var prev = await FindPreviousAsync(window)
            ?? throw new ConflictException($"There is no money left from the {window.Unit} before {window.Describe()} to move.");

        var action = Enum.Parse<CarryOverAction>(request.Action!);
        var amount = prev.Left;
        var record = new BudgetCarryOver
        {
            UserId = userId, FromBudgetId = prev.Budget.Id, ToBudgetId = to.Id, Action = action, Amount = amount, CreatedAt = clock.UtcNow,
        };
        string? goalName = null;

        await using var transaction = await db.Database.BeginTransactionAsync();
        switch (action)
        {
            case CarryOverAction.AddToBudget:
                to.Amount += amount;
                to.UpdatedAt = clock.UtcNow;
                if (request.Category is not null)
                {
                    var category = Enum.Parse<Category>(request.Category);
                    var limit = to.CategoryLimits.FirstOrDefault(l => l.Category == category)
                        ?? throw new ValidationFailedException(nameof(request.Category),
                            $"{category} has no limit in this budget. Choose a category with a limit, or none.");
                    limit.Amount += amount;
                    record.Category = category;
                }
                break;

            case CarryOverAction.MoveToSavings:
                var goal = await db.SavingsGoals.FirstOrDefaultAsync(g => g.Id == request.GoalId && g.UserId == userId)
                    ?? throw new ValidationFailedException(nameof(request.GoalId), "Choose one of your savings goals.");
                if (goal.ArchivedAt is not null)
                {
                    throw new ValidationFailedException(nameof(request.GoalId), "This goal is archived. Restore it before adding money.");
                }
                // Dated on the finished period's last day, so it comes out of that period's leftover.
                var deposit = new SavingsTransaction
                {
                    GoalId = goal.Id,
                    Type = SavingsTransactionType.Contribution,
                    Amount = amount,
                    Date = prev.Window.End,
                    Note = $"Left from {prev.Window.Describe()}",
                    CreatedAt = clock.UtcNow,
                };
                db.SavingsTransactions.Add(deposit);
                goal.UpdatedAt = clock.UtcNow;
                await db.SaveChangesAsync();
                record.SavingsTransactionId = deposit.Id;
                goalName = goal.Name;
                break;
        }

        db.BudgetCarryOvers.Add(record);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            throw new ConflictException("You already decided what to do with that money.");
        }
        return new CarryOverResponse(action.ToString(), amount, prev.Window.Start, prev.Window.End, record.Category?.ToString(), goalName);
    }

    private async Task<(Budget Budget, BudgetWindow Window, decimal Left)?> FindPreviousAsync(BudgetWindow window)
    {
        var userId = currentUser.Id;
        var prevWindow = BudgetWindow.For(window.Period, window.Start.AddDays(-1));
        // Not over yet, so what is left could still change.
        if (clock.Today() <= prevWindow.End) return null;

        var prev = await db.Budgets.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Period == window.Period && b.StartDate == prevWindow.Start);
        if (prev is null || await db.BudgetCarryOvers.AnyAsync(c => c.FromBudgetId == prev.Id)) return null;

        var left = await LeftAsync(db, userId, prev);
        return left > 0 ? (prev, prevWindow, left) : null;
    }
}
