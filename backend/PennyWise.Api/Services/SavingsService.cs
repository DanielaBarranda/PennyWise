using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// Savings for the signed-in user: goals (with a target) and plain funds the user
// names, like "Wallet" or "Envelope" (no target). Setting money aside is not
// spending, so it never changes spending totals or category limits.
//
// How savings touch a budget:
//   - A deposit "from my budget" comes out of the budget for its date; a deposit
//     "from somewhere else" (money already owned, a gift) does not.
//   - A withdrawal gives money back to the budget of the same period, up to what
//     was deposited from the budget in that period (see SetAsideAsync).
//   - A withdrawal recorded as an expense pays for that expense; the expense is
//     paid from savings, so it is left out of every budget and category limit
//     (see BudgetExpenses).
public class SavingsService(PennyWiseDbContext db, AppClock clock, CurrentUser currentUser)
{
    public async Task<List<SavingsGoalResponse>> ListAsync(bool includeArchived)
    {
        var userId = currentUser.Id;
        var goals = await db.SavingsGoals.AsNoTracking()
            .Include(g => g.Transactions)
            .Where(g => g.UserId == userId && (includeArchived || g.ArchivedAt == null))
            .OrderBy(g => g.ArchivedAt != null).ThenBy(g => g.CreatedAt)
            .ToListAsync();
        var today = clock.Today();
        return goals.Select(g => ToResponse(g, today)).ToList();
    }

    public async Task<SavingsGoalResponse> GetAsync(int id) => ToResponse(await FindAsync(id), clock.Today());

    public async Task<SavingsGoalResponse> CreateAsync(SavingsGoalRequest request)
    {
        var today = clock.Today();
        if (request.TargetDate < today)
        {
            throw new ValidationFailedException(nameof(request.TargetDate), "Target date can't be in the past.");
        }

        var now = clock.UtcNow;
        var goal = new SavingsGoal { UserId = currentUser.Id, CreatedAt = now };
        Apply(goal, request, now);
        // Money the user already has in it: it was never part of a budget.
        if (request.StartingAmount is > 0 and var starting)
        {
            goal.Transactions.Add(new SavingsTransaction
            {
                Type = SavingsTransactionType.Contribution,
                Amount = starting,
                Date = today,
                Note = "Starting amount",
                FromOutside = true,
                CreatedAt = now,
            });
        }
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        return ToResponse(goal, today);
    }

    public async Task<SavingsGoalResponse> UpdateAsync(int id, SavingsGoalRequest request)
    {
        var goal = await FindAsync(id);
        var today = clock.Today();
        // A past target date may be kept, but not newly chosen.
        if (request.TargetDate < today && request.TargetDate != goal.TargetDate)
        {
            throw new ValidationFailedException(nameof(request.TargetDate), "Target date can't be in the past.");
        }
        Apply(goal, request, clock.UtcNow);
        await db.SaveChangesAsync();
        return ToResponse(goal, today);
    }

    public async Task<SavingsGoalResponse> SetArchivedAsync(int id, bool archived)
    {
        var goal = await FindAsync(id);
        goal.ArchivedAt = archived ? clock.UtcNow : null;
        goal.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(goal, clock.Today());
    }

    public async Task<List<SavingsTransactionResponse>> ListTransactionsAsync(int goalId)
    {
        var goal = await FindAsync(goalId);
        return goal.Transactions
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<SavingsGoalResponse> AddTransactionAsync(int goalId, SavingsTransactionRequest request)
    {
        var goal = await FindAsync(goalId);
        var today = clock.Today();
        if (goal.ArchivedAt is not null)
        {
            throw new ValidationFailedException("Goal", "This goal is archived. Restore it before adding money.");
        }
        if (request.Date > today)
        {
            throw new ValidationFailedException(nameof(request.Date), "Date can't be in the future.");
        }

        var type = Enum.Parse<SavingsTransactionType>(request.Type!);
        var amount = request.Amount!.Value;
        if (type == SavingsTransactionType.Withdrawal && amount > Saved(goal))
        {
            throw new ValidationFailedException(nameof(request.Amount),
                $"You can withdraw at most {InsightService.Peso(Saved(goal))}, the amount saved in this goal.");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var transaction = new SavingsTransaction
        {
            Type = type,
            Amount = amount,
            Date = request.Date!.Value,
            Note = note,
            FromOutside = type == SavingsTransactionType.Contribution && request.FromOutside,
            CreatedAt = clock.UtcNow,
        };

        // Spending money straight from savings: record the expense too, linked to
        // this withdrawal, so it is not counted against a budget.
        if (type == SavingsTransactionType.Withdrawal && request.RecordExpense)
        {
            var expense = new Expense
            {
                UserId = currentUser.Id,
                Amount = amount,
                Category = Enum.Parse<Category>(request.Category!),
                Description = note ?? $"Paid from {goal.Name}",
                ExpenseDate = transaction.Date,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow,
            };
            db.Expenses.Add(expense);
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.SaveChangesAsync();
            transaction.ExpenseId = expense.Id;
            goal.Transactions.Add(transaction);
            goal.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return ToResponse(goal, today);
        }

        goal.Transactions.Add(transaction);
        goal.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        return ToResponse(goal, today);
    }

    private async Task<SavingsGoal> FindAsync(int id)
    {
        var userId = currentUser.Id;
        return await db.SavingsGoals.Include(g => g.Transactions).FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId)
            ?? throw new NotFoundException($"Savings goal {id} was not found.");
    }

    private static void Apply(SavingsGoal goal, SavingsGoalRequest request, DateTime now)
    {
        goal.Name = request.Name!.Trim();
        goal.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        goal.TargetAmount = request.TargetAmount;
        // A target date only means something with a target amount.
        goal.TargetDate = request.TargetAmount is null ? null : request.TargetDate;
        goal.UpdatedAt = now;
    }

    public static decimal Saved(SavingsGoal goal) => SavedUntil(goal, DateOnly.MaxValue);

    // The user's expenses that are paid from the budget: every expense except the
    // ones paid with money taken out of savings.
    public static IQueryable<Expense> BudgetExpenses(PennyWiseDbContext db, int userId) =>
        db.Expenses.AsNoTracking().Where(e => e.UserId == userId && !db.SavingsTransactions.Any(t => t.ExpenseId == e.Id));

    // For each of these expenses paid from savings: the name of the goal or fund.
    public static async Task<Dictionary<int, string>> PaidFromAsync(PennyWiseDbContext db, IEnumerable<int> expenseIds)
    {
        var ids = expenseIds.ToList();
        if (ids.Count == 0) return [];
        return await db.SavingsTransactions.AsNoTracking()
            .Where(t => t.ExpenseId != null && ids.Contains(t.ExpenseId.Value))
            .Join(db.SavingsGoals, t => t.GoalId, g => g.Id, (t, g) => new { Id = t.ExpenseId!.Value, g.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);
    }

    // Expenses between two dates that were paid from savings.
    public static async Task<decimal> SpentFromSavingsAsync(PennyWiseDbContext db, int userId, DateOnly first, DateOnly last) =>
        (await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= first && e.ExpenseDate <= last && db.SavingsTransactions.Any(t => t.ExpenseId == e.Id))
            .Select(e => e.Amount).ToListAsync()).Sum();

    // Money moved from the budget into savings between two dates: deposits from
    // the budget minus plain withdrawals, never below 0. It comes out of the budget
    // for those dates, like spending, but is not spending. Deposits from outside a
    // budget, and withdrawals that paid an expense, do not count.
    public static async Task<decimal> SetAsideAsync(PennyWiseDbContext db, int userId, DateOnly first, DateOnly last)
    {
        var transactions = await db.SavingsTransactions.AsNoTracking()
            .Where(t => t.Date >= first && t.Date <= last && db.SavingsGoals.Any(g => g.Id == t.GoalId && g.UserId == userId))
            .Where(t => t.Type == SavingsTransactionType.Contribution ? !t.FromOutside : t.ExpenseId == null)
            .Select(t => new { t.Type, t.Amount })
            .ToListAsync();
        var net = transactions.Sum(t => t.Type == SavingsTransactionType.Contribution ? t.Amount : -t.Amount);
        return Math.Max(net, 0);
    }

    // Saved amount counting only transactions dated on or before the given day
    // (used by monthly reports for "saved at the end of the month").
    public static decimal SavedUntil(SavingsGoal goal, DateOnly day) =>
        goal.Transactions.Where(t => t.Date <= day)
            .Sum(t => t.Type == SavingsTransactionType.Contribution ? t.Amount : -t.Amount);

    public static SavingsGoalResponse ToResponse(SavingsGoal goal, DateOnly today)
    {
        var saved = Saved(goal);
        var target = goal.TargetAmount;
        var percent = target > 0 ? Math.Round(saved / target.Value * 100, 1) : 0;
        var status = goal.ArchivedAt is not null ? "Archived" : target is not null && saved >= target ? "Completed" : "Active";

        int? daysLeft = goal.TargetDate is { } targetDate ? targetDate.DayNumber - today.DayNumber : null;
        decimal? expected = null;
        if (goal.TargetDate is { } end && target is { } amount)
        {
            var start = DateOnly.FromDateTime(goal.CreatedAt);
            var totalDays = end.DayNumber - start.DayNumber;
            var elapsed = Math.Clamp(today.DayNumber - start.DayNumber, 0, Math.Max(totalDays, 0));
            expected = totalDays > 0 ? Math.Round(amount * elapsed / totalDays, 2) : amount;
        }

        return new SavingsGoalResponse(goal.Id, goal.Name, goal.Description, target, saved,
            target is null ? 0 : Math.Max(target.Value - saved, 0), percent, goal.TargetDate, daysLeft, expected, status, goal.CreatedAt);
    }

    private static SavingsTransactionResponse ToResponse(SavingsTransaction t) =>
        new(t.Id, t.Type.ToString(), t.Amount, t.Date, t.Note, t.FromOutside, t.ExpenseId, t.CreatedAt);
}
