using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Services;

// Rule-based insights, calculated from the user's own records each time they
// are requested (no AI service, nothing hard-coded). Rules:
//   1. Bills overdue or due within 7 days (current month view only)
//   2. The month's overall budget: over it, or 80%+ used
//   3. Each category limit: over it, or 75%+ used (one insight per category)
//   4. Category spending compared with the same days of last month
//   5. Savings goals reached, or behind a target date
//   6. A suggestion to set a budget when there is spending but no budget
// "Days left" only appears for the current month. Comparisons use the same
// span of days in both months (1st to today), so a half-finished month is never
// compared with a whole one.
public class InsightService(PennyWiseDbContext db, AppClock clock, CurrentUser currentUser, BillService bills)
{
    // Invariant culture on purpose: the Linux server runs without ICU culture data
    // (InvariantGlobalization), so en-PH is not available there.
    private static readonly CultureInfo Ph = CultureInfo.InvariantCulture;
    private const decimal CategoryWatchPercent = 75m;
    private const decimal ComparisonMinPercent = 15m;
    private const decimal ComparisonMinAmount = 100m;

    public async Task<List<InsightResponse>> GetAsync(string month)
    {
        var userId = currentUser.Id;
        var (first, last) = MonthKey.Range(month);
        var today = clock.Today();
        var isCurrent = today >= first && today <= last;
        var daysLeft = isCurrent ? last.DayNumber - today.DayNumber + 1 : 0;
        var monthName = first.ToString("MMMM yyyy", Ph);
        var insights = new List<InsightResponse>();

        var budget = await db.Budgets.AsNoTracking().Include(b => b.CategoryLimits)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Month == month);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= first && e.ExpenseDate <= last)
            .Select(e => new { e.Amount, e.Category, e.ExpenseDate }).ToListAsync();
        var spent = expenses.Sum(e => e.Amount);

        // 1. Bills (only meaningful relative to today)
        if (isCurrent)
        {
            var occurrences = await bills.OccurrencesAsync(today.AddDays(-60), today.AddDays(BillService.DueSoonDays));
            foreach (var bill in occurrences.Where(o => o.Status == "Overdue").Take(3))
            {
                insights.Add(new($"bill-overdue-{bill.BillId}-{bill.DueDate:yyyyMMdd}", "Critical", "Fact", "Overdue bill",
                    $"Your {bill.Name} bill ({Peso(bill.Amount)}) was due on {bill.DueDate.ToString("MMM d", Ph)}.", "bills", false));
            }
            foreach (var bill in occurrences.Where(o => o.Status == "DueSoon").Take(3))
            {
                var when = bill.DaysUntilDue switch { 0 => "today", 1 => "tomorrow", var d => $"in {d} days" };
                insights.Add(new($"bill-due-{bill.BillId}-{bill.DueDate:yyyyMMdd}", bill.DaysUntilDue <= 3 ? "Warning" : "Info", "Fact",
                    "Bill due soon", $"Your {bill.Name} bill ({Peso(bill.Amount)}) is due {when}.", "bills", true));
            }
        }

        // 2. Overall budget
        if (budget is not null && budget.Amount > 0)
        {
            var percent = spent / budget.Amount * 100;
            if (spent > budget.Amount)
            {
                insights.Add(new($"budget-over-{month}", "Critical", "Fact", "Over budget",
                    $"You've spent {Peso(spent - budget.Amount)} more than your {monthName} budget of {Peso(budget.Amount)}.", "dashboard", false));
            }
            else if (percent >= 80)
            {
                insights.Add(new($"budget-warn-{month}", "Warning", "Fact", "Budget almost used",
                    $"You've used {Pct(percent)} of your {monthName} budget{DaysLeftText(daysLeft)}. {Peso(budget.Amount - spent)} is left.",
                    "dashboard", true));
            }
        }
        else if (isCurrent && expenses.Count > 0)
        {
            insights.Add(new($"no-budget-{month}", "Info", "Suggestion", "No budget yet",
                $"You've recorded {Peso(spent)} of spending this month. Setting a budget would show how much is left.", "dashboard", true));
        }

        // 3. Category limits (at most one insight per category)
        var flagged = new HashSet<Category>();
        foreach (var limit in budget?.CategoryLimits.Where(l => l.Amount > 0).OrderBy(l => l.Category) ?? Enumerable.Empty<CategoryLimit>())
        {
            var total = expenses.Where(e => e.Category == limit.Category).Sum(e => e.Amount);
            var percent = total / limit.Amount * 100;
            var name = limit.Category.ToString();
            if (total > limit.Amount)
            {
                flagged.Add(limit.Category);
                insights.Add(new($"cat-over-{month}-{name}", "Warning", "Fact", $"{name} is over its limit",
                    $"{name} spending is {Peso(total - limit.Amount)} over its {Peso(limit.Amount)} limit.", "expenses", true));
            }
            else if (percent >= CategoryWatchPercent)
            {
                flagged.Add(limit.Category);
                insights.Add(new($"cat-watch-{month}-{name}", "Warning", "Fact", $"{name} is close to its limit",
                    $"You've used {Pct(percent)} of your {name} limit{DaysLeftText(daysLeft)}. {Peso(limit.Amount - total)} remains.",
                    "expenses", true));
            }
        }

        // 4. Same days of last month, per category
        var span = isCurrent ? today.Day : last.Day;
        var prevFirst = first.AddMonths(-1);
        var prevEnd = prevFirst.AddDays(Math.Min(span, DateTime.DaysInMonth(prevFirst.Year, prevFirst.Month)) - 1);
        var thisEnd = first.AddDays(span - 1);
        var previous = await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= prevFirst && e.ExpenseDate <= prevEnd)
            .Select(e => new { e.Amount, e.Category }).ToListAsync();
        var comparisons = Enum.GetValues<Category>()
            .Where(c => !flagged.Contains(c))
            .Select(c => new
            {
                Category = c,
                Now = expenses.Where(e => e.Category == c && e.ExpenseDate <= thisEnd).Sum(e => e.Amount),
                Before = previous.Where(e => e.Category == c).Sum(e => e.Amount),
            })
            .Where(x => x.Before >= ComparisonMinAmount && Math.Abs(x.Now - x.Before) >= ComparisonMinAmount)
            .Select(x => new { x.Category, x.Now, x.Before, Change = (x.Now - x.Before) / x.Before * 100 })
            .Where(x => Math.Abs(x.Change) >= ComparisonMinPercent)
            .OrderByDescending(x => Math.Abs(x.Now - x.Before))
            .Take(2);
        var periodText = isCurrent ? $"in the first {span} day{(span == 1 ? "" : "s")} of the month" : "this month";
        foreach (var c in comparisons)
        {
            var up = c.Change > 0;
            insights.Add(new($"cmp-{month}-{c.Category}-{(up ? "up" : "down")}", "Info", "Fact",
                $"{c.Category} spending {(up ? "rose" : "fell")}",
                $"{c.Category} spending is {(up ? "up" : "down")} {Pct(Math.Abs(c.Change))} compared with the same days last month " +
                $"({Peso(c.Now)} {periodText}, against {Peso(c.Before)}).", "expenses", true));
        }

        // 5. Savings goals
        var goals = await db.SavingsGoals.AsNoTracking().Include(g => g.Transactions)
            .Where(g => g.UserId == userId && g.ArchivedAt == null).ToListAsync();
        foreach (var goal in goals)
        {
            var g = SavingsService.ToResponse(goal, today);
            if (g.Status == "Completed")
            {
                insights.Add(new($"goal-done-{goal.Id}", "Success", "Fact", "Savings goal reached",
                    $"You've reached your {goal.Name} goal of {Peso(goal.TargetAmount)}.", "savings", true));
            }
            else if (isCurrent && g.DaysLeft is > 0 and <= 90 && g.ExpectedByToday is { } expected && g.SavedAmount < expected)
            {
                var weeksLeft = Math.Max(1, (decimal)Math.Ceiling(g.DaysLeft.Value / 7.0));
                insights.Add(new($"goal-behind-{goal.Id}-{today:yyyyMMdd}", "Info", "Suggestion", "Savings goal pace",
                    $"To reach {goal.Name} by {goal.TargetDate!.Value.ToString("MMM d", Ph)}, setting aside about " +
                    $"{Peso(Math.Ceiling(g.RemainingAmount / weeksLeft))} a week would get you there.", "savings", true));
            }
            else if (isCurrent && g.DaysLeft < 0)
            {
                insights.Add(new($"goal-late-{goal.Id}", "Info", "Fact", "Savings goal date passed",
                    $"The target date for {goal.Name} has passed with {Peso(g.RemainingAmount)} still to go. You can set a new date.", "savings", true));
            }
        }

        var order = new Dictionary<string, int> { ["Critical"] = 0, ["Warning"] = 1, ["Success"] = 2, ["Info"] = 3 };
        return insights.OrderBy(i => order[i.Severity]).ToList();
    }

    public static string Peso(decimal amount) => (amount < 0 ? "-" : "") + "₱" + Math.Abs(amount).ToString("N2", Ph);

    private static string Pct(decimal percent) => $"{Math.Round(percent, 0, MidpointRounding.AwayFromZero)}%";

    private static string DaysLeftText(int daysLeft) => daysLeft switch
    {
        <= 0 => "",
        1 => ", with today the last day of the month",
        _ => $", with {daysLeft} days left in the month",
    };
}
