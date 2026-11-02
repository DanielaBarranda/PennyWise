namespace PennyWise.Api.Models;

// A budget for one day, one week (Monday to Sunday) or one calendar month.
// Each period stands on its own: a weekly budget is never turned into a
// monthly one or the other way round.
public class Budget
{
    public int Id { get; set; }

    // The account that owns this record. Null only for records created before
    // accounts existed; those stay hidden until claimed by the configured owner
    // (see Auth/LegacyDataClaimer.cs).
    public int? UserId { get; set; }

    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monthly;

    // The first day of the period: the day itself, the Monday of the week, or
    // the 1st of the month. Unique per user and period, so a period never has two budgets.
    public DateOnly StartDate { get; set; }

    // "YYYY-MM" of StartDate, kept so a month's budgets are easy to list.
    public string Month { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public List<CategoryLimit> CategoryLimits { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public enum BudgetPeriod
{
    Daily,
    Weekly,
    Monthly,
}

// Optional spending cap for one category within a budget's period.
public class CategoryLimit
{
    public int Id { get; set; }

    public int BudgetId { get; set; }

    public Category Category { get; set; }

    public decimal Amount { get; set; }
}
