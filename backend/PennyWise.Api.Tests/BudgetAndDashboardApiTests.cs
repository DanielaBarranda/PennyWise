using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class BudgetAndDashboardApiTests : ApiTestBase
{
    [Fact]
    public async Task Month_without_budget_reports_NoBudget_and_null_remaining()
    {
        await CreateExpenseAsync(300, "Food", "2026-10-02");

        var summary = await GetSummaryAsync("2026-10");

        Assert.Null(summary.Budget);
        Assert.Equal("NoBudget", summary.Status);
        Assert.Null(summary.Remaining);
        Assert.Null(summary.PercentSpent);
        Assert.Equal(300m, summary.TotalSpent);
        Assert.Equal(6, summary.Categories.Count); // always all six categories
    }

    [Fact]
    public async Task Totals_use_exact_decimal_arithmetic()
    {
        await CreateBudgetAsync("2026-10", 1);
        await CreateExpenseAsync(0.10m, "Transportation", "2026-10-01");
        await CreateExpenseAsync(0.20m, "Transportation", "2026-10-01");

        var summary = await GetSummaryAsync("2026-10");

        // With double this would be 0.30000000000000004.
        Assert.Equal(0.30m, summary.TotalSpent);
        Assert.Equal(0.70m, summary.Remaining);
    }

    [Theory]
    [InlineData(799.99, "OnTrack")]
    [InlineData(800.00, "Warning")] // exactly 80%
    [InlineData(1000.00, "Warning")] // exactly 100% is not over yet
    [InlineData(1000.01, "Exceeded")]
    public async Task Budget_status_follows_80_and_100_percent_thresholds(decimal spent, string expectedStatus)
    {
        await CreateBudgetAsync("2026-10", 1000);
        await CreateExpenseAsync(spent, "Bills", "2026-10-10");

        var summary = await GetSummaryAsync("2026-10");

        Assert.Equal(expectedStatus, summary.Status);
        Assert.Equal(1000m - spent, summary.Remaining);
    }

    [Fact]
    public async Task Dashboard_calculates_totals_percent_and_category_limits()
    {
        await CreateBudgetAsync("2026-10", 8000, ("Food", 3000), ("School", 4000));
        await CreateExpenseAsync(1800.50m, "Food", "2026-10-03");
        await CreateExpenseAsync(1250.25m, "Food", "2026-10-10");
        await CreateExpenseAsync(3200m, "School", "2026-10-31");
        await CreateExpenseAsync(999m, "Bills", "2026-11-01"); // next month: must be ignored

        var summary = await GetSummaryAsync("2026-10");

        Assert.Equal(6250.75m, summary.TotalSpent);
        Assert.Equal(1749.25m, summary.Remaining);
        Assert.Equal(78.13m, summary.PercentSpent);
        Assert.Equal("OnTrack", summary.Status);
        Assert.Equal(3, summary.ExpenseCount);

        var food = summary.Categories.Single(c => c.Category == "Food");
        Assert.Equal(3050.75m, food.Total);
        Assert.Equal("Exceeded", food.Status);
        Assert.Equal("Warning", summary.Categories.Single(c => c.Category == "School").Status); // 80%
        Assert.Equal("NoLimit", summary.Categories.Single(c => c.Category == "Bills").Status);
    }

    [Fact]
    public async Task Recent_expenses_are_newest_first_and_limited_to_five()
    {
        for (var day = 1; day <= 7; day++) await CreateExpenseAsync(day, "Others", $"2026-10-0{day}");

        var summary = await GetSummaryAsync("2026-10");

        Assert.Equal([7m, 6m, 5m, 4m, 3m], summary.RecentExpenses.Select(e => e.Amount));
        Assert.Equal(7, summary.ExpenseCount);
    }

    [Fact]
    public async Task Second_budget_for_same_month_returns_409()
    {
        await CreateBudgetAsync("2026-10", 5000);

        var response = await Client.PostAsJsonAsync("/api/budgets", new { month = "2026-10", amount = 6000 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Category_limits_above_budget_are_rejected()
    {
        var response = await Client.PostAsJsonAsync("/api/budgets", new
        {
            month = "2026-10",
            amount = 1000,
            categoryLimits = new[] { new { category = "Food", amount = 1500 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Updating_budget_replaces_category_limits()
    {
        var budget = await CreateBudgetAsync("2026-10", 5000, ("Food", 2000), ("Bills", 1000));

        var response = await Client.PutAsJsonAsync($"/api/budgets/{budget.Id}", new
        {
            month = "2026-10",
            amount = 6000,
            categoryLimits = new[] { new { category = "Food", amount = 2500 }, new { category = "School", amount = 500 } },
        });
        var updated = await response.Content.ReadFromJsonAsync<BudgetResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(6000m, updated!.Amount);
        Assert.Equal(
            [("Food", 2500m), ("School", 500m)],
            updated.CategoryLimits.Select(l => (l.Category, l.Amount)));
    }
}
