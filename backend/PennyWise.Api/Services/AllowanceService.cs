using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Services;

// What can still be spent in one budget period (a day, a week or a month). The
// period's own budget is the limit; nothing is converted from another period.
//
//   available = budget - spent in the period - moved into savings in the period
//               - added to the next period's budget (see CarryOverService)
//               - unpaid bills due in the period (overdue ones included)
//   daily     = available / days left in the period (today included), rounded down to the centavo
//   total     = available
//
// Unpaid bills are set aside because they have not been spent yet. Paid bills
// that were recorded as expenses are already in "spent", so nothing is counted twice.
public class AllowanceService(PennyWiseDbContext db, AppClock clock, CurrentUser currentUser, BillService bills)
{
    public async Task<AllowanceResponse> CalculateAsync(BudgetWindow window)
    {
        var userId = currentUser.Id;
        var (first, last) = (window.Start, window.End);
        var today = clock.Today();

        var row = await db.Budgets.AsNoTracking()
            .Where(b => b.UserId == userId && b.Period == window.Period && b.StartDate == window.Start)
            .Select(b => new { b.Id, b.Amount }).FirstOrDefaultAsync();
        var budget = row?.Amount;
        var spent = (await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= first && e.ExpenseDate <= last)
            .Select(e => e.Amount).ToListAsync()).Sum();
        var saved = await SavingsService.SetAsideAsync(db, userId, first, last);

        var ended = today > last;
        // Unpaid bills due in the period: from its first day (overdue ones still
        // have to be paid) to its last. Nothing is set aside once it has ended.
        var reserved = ended
            ? 0m
            : (await bills.OccurrencesAsync(first, last)).Where(o => o.Status != "Paid").Sum(o => o.Amount);
        var daysLeft = ended ? 0 : last.DayNumber - (today < first ? first : today).DayNumber + 1;

        if (budget is null)
        {
            return new AllowanceResponse(window.Period.ToString(), first, last, "NoBudget", null, spent, saved, reserved, null, daysLeft, null, null);
        }

        var available = budget.Value - spent - saved - reserved - await CarryOverService.CarriedOutAsync(db, row!.Id);
        var spendable = Math.Max(available, 0);
        var daily = daysLeft > 0 ? Math.Floor(spendable / daysLeft * 100) / 100 : 0;
        var status = ended ? "Ended" : available < 0 ? "OverBudget" : "Available";

        return new AllowanceResponse(window.Period.ToString(), first, last, status, budget, spent, saved, reserved, available, daysLeft, daily, spendable);
    }
}
