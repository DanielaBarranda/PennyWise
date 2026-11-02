using System.Globalization;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// The dates a budget period covers: one day, one week (Monday to Sunday), or
// one calendar month. Every budget, dashboard, allowance and insight works on
// exactly one window, so nothing is converted between periods.
public readonly record struct BudgetWindow(BudgetPeriod Period, DateOnly Start, DateOnly End)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static BudgetWindow For(BudgetPeriod period, DateOnly date) => period switch
    {
        BudgetPeriod.Daily => new(period, date, date),
        BudgetPeriod.Weekly => new(period, MondayOf(date), MondayOf(date).AddDays(6)),
        _ => new(period, new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, 1).AddMonths(1).AddDays(-1)),
    };

    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    // True when `date` is the first day of a period (e.g. a Monday for a week).
    public static bool IsStart(BudgetPeriod period, DateOnly date) => For(period, date).Start == date;

    public string Month => Start.ToString("yyyy-MM", Invariant);

    public int Days => End.DayNumber - Start.DayNumber + 1;

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    // "October 2026", "the week of Nov 2" or "Nov 2", for messages.
    public string Describe() => Period switch
    {
        BudgetPeriod.Daily => Start.ToString("MMM d", Invariant),
        BudgetPeriod.Weekly => "the week of " + Start.ToString("MMM d", Invariant),
        _ => Start.ToString("MMMM yyyy", Invariant),
    };

    // "day", "week" or "month".
    public string Unit => Period switch { BudgetPeriod.Daily => "day", BudgetPeriod.Weekly => "week", _ => "month" };
}
