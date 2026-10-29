using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Services;

// Suggested spending allowances. The monthly budget is always the limit; the
// daily and weekly figures only spread what is left of it over the days left,
// so switching views never adds money or changes the budget.
//
//   available = budget - spent so far this month - unpaid bills due this month (overdue included)
//   daily     = available / days left in the month (today included)
//   weekly    = daily x days left in this week (Monday-Sunday), within this month
//   monthly   = available
//
// Unpaid bills are set aside because they have not been spent yet. Paid bills
// that were recorded as expenses are already in "spent", so nothing is counted twice.
public class AllowanceService(PennyWiseDbContext db, AppClock clock, CurrentUser currentUser, BillService bills)
{
    public async Task<AllowanceResponse> CalculateAsync(string month, AllowancePeriod? requested)
    {
        var userId = currentUser.Id;
        var period = requested ?? await db.Users.Where(u => u.Id == userId).Select(u => u.AllowancePeriod).FirstAsync();
        var (first, last) = MonthKey.Range(month);
        var today = clock.Today();

        var budget = await db.Budgets.AsNoTracking().Where(b => b.UserId == userId && b.Month == month)
            .Select(b => (decimal?)b.Amount).FirstOrDefaultAsync();
        var spent = (await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= first && e.ExpenseDate <= last)
            .Select(e => e.Amount).ToListAsync()).Sum();

        var timing = today > last ? "Past" : today < first ? "Future" : "Current";
        // Every unpaid bill due this month, including overdue ones: they still have to be paid.
        var reserved = timing == "Past"
            ? 0m
            : (await bills.OccurrencesAsync(first, last)).Where(o => o.Status != "Paid").Sum(o => o.Amount);

        var daysLeft = timing switch
        {
            "Current" => last.DayNumber - today.DayNumber + 1,
            "Future" => last.DayNumber - first.DayNumber + 1,
            _ => 0,
        };
        // Days from today (or the 1st, for a future month) to Sunday, not past the month's end.
        var weekStart = timing == "Current" ? today : first;
        var daysToSunday = ((int)DayOfWeek.Sunday - (int)weekStart.DayOfWeek + 7) % 7 + 1;
        var daysInWeek = timing == "Past" ? 0 : Math.Min(daysToSunday, daysLeft);

        if (budget is null)
        {
            return new AllowanceResponse(month, period.ToString(), "NoBudget", null, spent, reserved, null,
                daysLeft, daysInWeek, null, null, null, null);
        }

        var available = budget.Value - spent - reserved;
        var spendable = Math.Max(available, 0);
        // Rounded down to the centavo so the suggestions never add up to more than is available.
        var daily = daysLeft > 0 ? Math.Floor(spendable / daysLeft * 100) / 100 : 0;
        var weekly = Math.Min(daily * daysInWeek, spendable);
        var status = timing == "Past" ? "MonthEnded" : available < 0 ? "OverBudget" : "Available";
        var amount = period switch
        {
            AllowancePeriod.Daily => daily,
            AllowancePeriod.Weekly => weekly,
            _ => spendable,
        };

        return new AllowanceResponse(month, period.ToString(), status, budget, spent, reserved, available,
            daysLeft, daysInWeek, amount, daily, weekly, spendable);
    }
}
