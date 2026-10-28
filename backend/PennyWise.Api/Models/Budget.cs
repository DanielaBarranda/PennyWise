namespace PennyWise.Api.Models;

// One budget per calendar month.
public class Budget
{
    public int Id { get; set; }

    // "YYYY-MM", e.g. "2026-10". Unique, so a month can never have two budgets.
    public string Month { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public List<CategoryLimit> CategoryLimits { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

// Optional spending cap for one category within a budget month.
public class CategoryLimit
{
    public int Id { get; set; }

    public int BudgetId { get; set; }

    public Category Category { get; set; }

    public decimal Amount { get; set; }
}
