namespace PennyWise.Api.DTOs;

public record DashboardSummaryResponse(
    string Period,   // Daily | Weekly | Monthly
    DateOnly Start,
    DateOnly End,
    string Month,    // "YYYY-MM" of Start
    BudgetResponse? Budget,
    decimal TotalSpent,
    decimal? Remaining,
    decimal? PercentSpent,
    string Status,
    int ExpenseCount,
    List<CategorySummary> Categories,
    List<ExpenseResponse> RecentExpenses);

public record CategorySummary(string Category, decimal Total, decimal? Limit, string Status);
