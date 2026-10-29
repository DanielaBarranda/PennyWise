using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// Savings goals for the signed-in user. Contributions and withdrawals are kept
// separate from expenses: setting money aside is not spending, so it never
// changes budgets, dashboard totals or allowances.
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

        goal.Transactions.Add(new SavingsTransaction
        {
            Type = type,
            Amount = amount,
            Date = request.Date!.Value,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = clock.UtcNow,
        });
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
        goal.TargetAmount = request.TargetAmount!.Value;
        goal.TargetDate = request.TargetDate;
        goal.UpdatedAt = now;
    }

    public static decimal Saved(SavingsGoal goal) => SavedUntil(goal, DateOnly.MaxValue);

    // Saved amount counting only transactions dated on or before the given day
    // (used by monthly reports for "saved at the end of the month").
    public static decimal SavedUntil(SavingsGoal goal, DateOnly day) =>
        goal.Transactions.Where(t => t.Date <= day)
            .Sum(t => t.Type == SavingsTransactionType.Contribution ? t.Amount : -t.Amount);

    public static SavingsGoalResponse ToResponse(SavingsGoal goal, DateOnly today)
    {
        var saved = Saved(goal);
        var percent = goal.TargetAmount > 0 ? Math.Round(saved / goal.TargetAmount * 100, 1) : 0;
        var status = goal.ArchivedAt is not null ? "Archived" : saved >= goal.TargetAmount ? "Completed" : "Active";

        int? daysLeft = goal.TargetDate is { } target ? target.DayNumber - today.DayNumber : null;
        decimal? expected = null;
        if (goal.TargetDate is { } end)
        {
            var start = DateOnly.FromDateTime(goal.CreatedAt);
            var totalDays = end.DayNumber - start.DayNumber;
            var elapsed = Math.Clamp(today.DayNumber - start.DayNumber, 0, Math.Max(totalDays, 0));
            expected = totalDays > 0 ? Math.Round(goal.TargetAmount * elapsed / totalDays, 2) : goal.TargetAmount;
        }

        return new SavingsGoalResponse(goal.Id, goal.Name, goal.Description, goal.TargetAmount, saved,
            Math.Max(goal.TargetAmount - saved, 0), percent, goal.TargetDate, daysLeft, expected, status, goal.CreatedAt);
    }

    private static SavingsTransactionResponse ToResponse(SavingsTransaction t) =>
        new(t.Id, t.Type.ToString(), t.Amount, t.Date, t.Note, t.CreatedAt);
}
