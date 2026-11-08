using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Money left at the end of last week (Oct 12 - 18; today is Tue Oct 20).
public class CarryOverApiTests : FeatureTestBase
{
    private const string ThisWeek = "/api/dashboard/summary?period=Weekly&date=2026-10-20";
    private const string LastWeek = "/api/dashboard/summary?period=Weekly&date=2026-10-15";

    private async Task<(BudgetResponse Last, BudgetResponse This)> TwoWeeksAsync(object[]? limits = null)
    {
        var last = await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-12", amount = 1000 });
        await CreateExpenseAsync(700, "Food", "2026-10-14");
        await CreateExpenseAsync(100, "School", "2026-10-18");
        var current = await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount = 1000, categoryLimits = limits ?? [] });
        return (last, current);
    }

    private async Task<DashboardSummaryResponse> SummaryAsync(string url) =>
        (await Client.GetFromJsonAsync<DashboardSummaryResponse>(url))!;

    [Fact]
    public async Task This_weeks_dashboard_offers_what_was_left_last_week()
    {
        var (last, _) = await TwoWeeksAsync();

        var offer = (await SummaryAsync(ThisWeek)).LeftoverOffer;

        Assert.NotNull(offer);
        Assert.Equal((last.Id, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), 200m), (offer!.FromBudgetId, offer.Start, offer.End, offer.Amount));
    }

    [Fact]
    public async Task Adding_it_to_this_week_raises_the_budget_and_uses_it_up_last_week()
    {
        await TwoWeeksAsync([new { category = "Food", amount = 400 }]);
        var current = (await SummaryAsync(ThisWeek)).Budget!;

        var result = await PostAsync<CarryOverResponse>($"/api/budgets/{current.Id}/carryover", new { action = "AddToBudget", category = "Food" });

        Assert.Equal(("AddToBudget", 200m, "Food"), (result.Action, result.Amount, result.Category));
        var now = await SummaryAsync(ThisWeek);
        Assert.Equal((1200m, 600m, 200m, 1200m), (now.Budget!.Amount, now.Budget.CategoryLimits.Single().Amount, now.CarriedIn, now.Remaining!.Value));
        Assert.Null(now.LeftoverOffer);
        var before = await SummaryAsync(LastWeek);
        Assert.Equal((200m, 0m), (before.CarriedOut, before.Remaining!.Value));
        var allowance = await Client.GetFromJsonAsync<AllowanceResponse>("/api/allowance?period=Weekly&date=2026-10-20");
        Assert.Equal(1200m, allowance!.Available);
    }

    [Fact]
    public async Task Moving_it_to_savings_adds_a_deposit_on_last_weeks_final_day()
    {
        await TwoWeeksAsync();
        var current = (await SummaryAsync(ThisWeek)).Budget!;
        var goal = await CreateGoalAsync("Treadmill", 5000);

        var result = await PostAsync<CarryOverResponse>($"/api/budgets/{current.Id}/carryover", new { action = "MoveToSavings", goalId = goal.Id });

        Assert.Equal(("MoveToSavings", 200m, "Treadmill"), (result.Action, result.Amount, result.GoalName));
        var saved = await Client.GetFromJsonAsync<SavingsGoalResponse>($"/api/savings/{goal.Id}");
        Assert.Equal(200m, saved!.SavedAmount);
        var before = await SummaryAsync(LastWeek);
        Assert.Equal((200m, 0m), (before.Saved, before.Remaining!.Value));
        var now = await SummaryAsync(ThisWeek);
        Assert.Equal((1000m, 1000m, 0m), (now.Budget!.Amount, now.Remaining!.Value, now.Saved));
        Assert.Null(now.LeftoverOffer);
    }

    [Fact]
    public async Task Leaving_it_changes_nothing_and_stops_the_question()
    {
        await TwoWeeksAsync();
        var current = (await SummaryAsync(ThisWeek)).Budget!;

        await PostAsync<CarryOverResponse>($"/api/budgets/{current.Id}/carryover", new { action = "Keep" });

        var now = await SummaryAsync(ThisWeek);
        Assert.Equal((1000m, (LeftoverOffer?)null), (now.Budget!.Amount, now.LeftoverOffer));
        Assert.Equal(200m, (await SummaryAsync(LastWeek)).Remaining);
        var again = await Client.PostAsJsonAsync($"/api/budgets/{current.Id}/carryover", new { action = "AddToBudget" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Nothing_is_offered_when_last_week_went_over_or_has_no_budget()
    {
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-12", amount = 500 });
        await CreateExpenseAsync(600, "Food", "2026-10-14");
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount = 1000 });
        Assert.Null((await SummaryAsync(ThisWeek)).LeftoverOffer);

        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-26", amount = 1000 });
        // Next week: this week is not over yet.
        Assert.Null((await SummaryAsync("/api/dashboard/summary?period=Weekly&date=2026-10-27")).LeftoverOffer);
    }

    [Fact]
    public async Task A_category_without_a_limit_is_refused_and_nothing_changes()
    {
        await TwoWeeksAsync();
        var current = (await SummaryAsync(ThisWeek)).Budget!;

        var response = await Client.PostAsJsonAsync($"/api/budgets/{current.Id}/carryover", new { action = "AddToBudget", category = "Food" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var now = await SummaryAsync(ThisWeek);
        Assert.Equal((1000m, 200m), (now.Budget!.Amount, now.LeftoverOffer!.Amount));
    }

    [Fact]
    public async Task The_report_shows_money_moved_between_weeks_once()
    {
        await TwoWeeksAsync();
        var current = (await SummaryAsync(ThisWeek)).Budget!;
        await PostAsync<CarryOverResponse>($"/api/budgets/{current.Id}/carryover", new { action = "AddToBudget" });

        var report = await Client.GetFromJsonAsync<MonthlyReportResponse>("/api/reports/monthly?month=2026-10");

        var weeks = report!.PeriodBudgets.Select(b => (b.Start.Day, b.Amount, b.CarriedIn, b.CarriedOut, b.Left)).ToList();
        Assert.Equal([(12, 1000m, 0m, 200m, 0m), (19, 1200m, 200m, 0m, 1200m)], weeks);
    }

    [Fact]
    public async Task Another_users_budget_cannot_receive_the_money()
    {
        var (_, current) = await TwoWeeksAsync();
        var other = await NewUserAsync("other@pennywise.test");

        var response = await other.PostAsJsonAsync($"/api/budgets/{current.Id}/carryover", new { action = "AddToBudget" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
