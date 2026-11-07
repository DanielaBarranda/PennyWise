namespace PennyWise.Api.DTOs;

public record DashboardSummaryResponse(
    string Period,   // Daily | Weekly | Monthly
    DateOnly Start,
    DateOnly End,
    string Month,    // "YYYY-MM" of Start
    BudgetResponse? Budget,
    decimal TotalSpent,
    decimal Saved,           // moved into savings goals in the period (deposits - withdrawals, at least 0)
    decimal? Remaining,      // budget - spent - saved; negative when over budget
    decimal? PercentSpent,   // (spent + saved) / budget
    string Status,
    int ExpenseCount,
    List<CategorySummary> Categories,
    List<ExpenseResponse> RecentExpenses);

public record CategorySummary(string Category, decimal Total, decimal? Limit, string Status);
