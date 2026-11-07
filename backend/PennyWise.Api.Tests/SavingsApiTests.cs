using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class SavingsApiTests : FeatureTestBase
{
    [Fact]
    public async Task Deposits_and_withdrawals_give_an_exact_balance_and_history()
    {
        var goal = await CreateGoalAsync("Laptop", 30000, "2027-03-31");
        await AddSavingsAsync(goal.Id, "Contribution", 1000.10m, "2026-10-01");
        await AddSavingsAsync(goal.Id, "Contribution", 500.20m, "2026-10-10");
        var updated = await AddSavingsAsync(goal.Id, "Withdrawal", 300.30m, "2026-10-15");
        var history = await Client.GetFromJsonAsync<List<SavingsTransactionResponse>>($"/api/savings/{goal.Id}/transactions");

        Assert.Equal(1200.00m, updated.SavedAmount);
        Assert.Equal(28800.00m, updated.RemainingAmount);
        Assert.Equal(4.0m, updated.PercentSaved);
        Assert.Equal("Active", updated.Status);
        Assert.Equal(162, updated.DaysLeft); // Oct 20 -> Mar 31
        Assert.Equal(["Withdrawal", "Contribution", "Contribution"], history!.Select(t => t.Type)); // newest first
    }

    [Fact]
    public async Task Withdrawing_more_than_is_saved_is_rejected()
    {
        var goal = await CreateGoalAsync("Trip", 5000);
        await AddSavingsAsync(goal.Id, "Contribution", 200);

        var response = await Client.PostAsJsonAsync($"/api/savings/{goal.Id}/transactions", new { type = "Withdrawal", amount = 200.01, date = "2026-10-20" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Amount", (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);
        Assert.Equal(200m, (await Client.GetFromJsonAsync<SavingsGoalResponse>($"/api/savings/{goal.Id}"))!.SavedAmount);
    }

    [Theory]
    [InlineData(0, null, "TargetAmount")]
    [InlineData(-50, null, "TargetAmount")]
    [InlineData(1000, "2026-10-19", "TargetDate")] // yesterday
    public async Task Invalid_goals_are_rejected(decimal target, string? targetDate, string field)
    {
        var response = await Client.PostAsJsonAsync("/api/savings", new { name = "Bad", targetAmount = target, targetDate });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);
    }

    [Fact]
    public async Task Future_dated_deposits_are_rejected()
    {
        var goal = await CreateGoalAsync("Phone", 15000);
        var response = await Client.PostAsJsonAsync($"/api/savings/{goal.Id}/transactions", new { type = "Contribution", amount = 100, date = "2026-10-21" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reaching_the_target_marks_the_goal_completed()
    {
        var goal = await CreateGoalAsync("Emergency fund", 1000);
        var done = await AddSavingsAsync(goal.Id, "Contribution", 1000);

        Assert.Equal("Completed", done.Status);
        Assert.Equal(0m, done.RemainingAmount);
        Assert.Equal(100m, done.PercentSaved);
    }

    [Fact]
    public async Task Goals_can_be_edited_archived_and_restored()
    {
        var goal = await CreateGoalAsync("Bike", 8000);
        var edited = await Client.PutAsJsonAsync($"/api/savings/{goal.Id}", new { name = "Road bike", targetAmount = 9000, description = "Commuting" });
        await Client.PostAsync($"/api/savings/{goal.Id}/archive", null);

        var active = await Client.GetFromJsonAsync<List<SavingsGoalResponse>>("/api/savings");
        var all = await Client.GetFromJsonAsync<List<SavingsGoalResponse>>("/api/savings?includeArchived=true");
        var depositWhileArchived = await Client.PostAsJsonAsync($"/api/savings/{goal.Id}/transactions", new { type = "Contribution", amount = 10, date = "2026-10-20" });
        var restored = await PostAsync<SavingsGoalResponse>($"/api/savings/{goal.Id}/restore", new { });

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Empty(active!);
        Assert.Equal(("Road bike", 9000m, "Archived"), (all!.Single().Name, all.Single().TargetAmount, all.Single().Status));
        Assert.Equal(HttpStatusCode.BadRequest, depositWhileArchived.StatusCode);
        Assert.Equal("Active", restored.Status);
    }

    [Fact]
    public async Task Saving_money_comes_out_of_the_budget_but_is_not_spending()
    {
        await CreateBudgetAsync("2026-10", 5000, ("Food", 1000));
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 2000);

        var summary = await GetSummaryAsync("2026-10");

        Assert.Equal((0m, 2000m, 3000m, 40.00m), (summary.TotalSpent, summary.Saved, summary.Remaining!.Value, summary.PercentSpent!.Value));
        Assert.All(summary.Categories, c => Assert.Equal(0m, c.Total)); // category limits only count spending
    }

    [Fact]
    public async Task A_withdrawal_in_the_same_period_gives_the_money_back_to_the_budget()
    {
        await CreateBudgetAsync("2026-10", 5000);
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 2000, "2026-10-05");
        await AddSavingsAsync(goal.Id, "Withdrawal", 500, "2026-10-12");

        var summary = await GetSummaryAsync("2026-10");

        Assert.Equal((1500m, 3500m), (summary.Saved, summary.Remaining!.Value));
    }

    [Fact]
    public async Task Withdrawing_money_saved_earlier_does_not_add_to_a_budget()
    {
        await CreateBudgetAsync("2026-10", 5000);
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 2000, "2026-10-01");
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount = 1000 });
        await AddSavingsAsync(goal.Id, "Withdrawal", 800, "2026-10-20");

        var week = await Client.GetFromJsonAsync<DashboardSummaryResponse>("/api/dashboard/summary?period=Weekly&date=2026-10-20");

        Assert.Equal((0m, 1000m), (week!.Saved, week.Remaining!.Value));
    }

    [Fact]
    public async Task Savings_come_out_of_the_allowance_for_their_own_week_only()
    {
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount = 1000 });
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 300, "2026-10-16"); // last week
        await AddSavingsAsync(goal.Id, "Contribution", 250, "2026-10-20"); // this week
        await CreateExpenseAsync(100, "Food", "2026-10-20");

        var a = await Client.GetFromJsonAsync<AllowanceResponse>("/api/allowance?period=Weekly&date=2026-10-20");

        Assert.Equal((100m, 250m, 650m), (a!.Spent, a.Saved, a.Available!.Value));
    }

    [Fact]
    public async Task Saving_past_the_budget_shows_the_over_budget_insight()
    {
        await CreateBudgetAsync("2026-10", 1000);
        await CreateExpenseAsync(600, "Food", "2026-10-10");
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 500);

        var insights = await Client.GetFromJsonAsync<List<InsightResponse>>("/api/insights?month=2026-10");

        var over = Assert.Single(insights!, i => i.Key == "budget-over-2026-10");
        Assert.Contains("₱100.00 more than", over.Message);
    }

    [Fact]
    public async Task The_report_counts_savings_in_each_weekly_budget()
    {
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount = 1000 });
        await CreateExpenseAsync(200, "Food", "2026-10-20");
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 300, "2026-10-20");

        var report = await Client.GetFromJsonAsync<MonthlyReportResponse>("/api/reports/monthly?month=2026-10");

        var week = Assert.Single(report!.PeriodBudgets);
        Assert.Equal((200m, 300m, 500m), (week.Spent, week.Saved, week.Left));
    }

    [Fact]
    public async Task Other_users_cannot_see_or_change_a_goal()
    {
        var goal = await CreateGoalAsync("Private", 1000);
        var bob = await NewUserAsync("bob@pennywise.test");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/savings/{goal.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/savings/{goal.Id}/transactions",
            new { type = "Contribution", amount = 10, date = "2026-10-20" })).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<List<SavingsGoalResponse>>("/api/savings"))!);
    }
}
