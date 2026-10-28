namespace PennyWise.Api.DTOs;

public record DashboardSummaryResponse(
    string Month,
    BudgetResponse? Budget,
    decimal TotalSpent,
    decimal? Remaining,
    decimal? PercentSpent,
    string Status,
    int ExpenseCount,
    List<CategorySummary> Categories,
    List<ExpenseResponse> RecentExpenses);

public record CategorySummary(string Category, decimal Total, decimal? Limit, string Status);
