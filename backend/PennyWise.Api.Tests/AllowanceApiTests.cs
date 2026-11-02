using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Today is Tue 20 Oct 2026: 12 days left in October (today included), and the
// week of Mon 19 Oct runs to Sun 25 Oct, so 6 days are left in it.
public class AllowanceApiTests : FeatureTestBase
{
    private async Task<AllowanceResponse> GetAllowanceAsync(string query = "month=2026-10") =>
        (await Client.GetFromJsonAsync<AllowanceResponse>($"/api/allowance?{query}"))!;

    [Fact]
    public async Task Monthly_allowance_spreads_what_is_left_after_spending_and_unpaid_bills()
    {
        await CreateBudgetAsync("2026-10", 6000);
        await CreateExpenseAsync(1000, "Food", "2026-10-05");
        await CreateBillAsync("Water", 600, "OneTime", "2026-10-25");

        var a = await GetAllowanceAsync();

        Assert.Equal((6000m, 1000m, 600m, 4400m), (a.Budget!.Value, a.Spent, a.ReservedForBills, a.Available!.Value));
        Assert.Equal(("Monthly", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), (a.Period, a.Start, a.End));
        Assert.Equal(12, a.DaysLeft);
        Assert.Equal(366.66m, a.Daily); // 4400 / 12, rounded down
        Assert.Equal(4400m, a.Total);
        Assert.Equal("Available", a.Status);
    }

    [Fact]
    public async Task Weekly_allowance_uses_only_the_weeks_own_budget_and_spending()
    {
        await CreateBudgetAsync("2026-10", 6000); // a monthly budget is ignored in the weekly view
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount = 1000 });
        await CreateExpenseAsync(400, "Food", "2026-10-18");  // Sunday before: last week
        await CreateExpenseAsync(100, "Food", "2026-10-19");  // this week
        await CreateBillAsync("Load", 300, "OneTime", "2026-10-24"); // due this week
        await CreateBillAsync("Rent", 2000, "OneTime", "2026-10-28"); // next week

        var a = await GetAllowanceAsync("period=Weekly&date=2026-10-20");

        Assert.Equal(("Weekly", new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)), (a.Period, a.Start, a.End));
        Assert.Equal((1000m, 100m, 300m, 600m), (a.Budget!.Value, a.Spent, a.ReservedForBills, a.Available!.Value));
        Assert.Equal((6, 100m, 600m), (a.DaysLeft, a.Daily!.Value, a.Total!.Value));
    }

    [Fact]
    public async Task Daily_allowance_is_whatever_is_left_today()
    {
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Daily", start = "2026-10-20", amount = 300 });
        await CreateExpenseAsync(120, "Food", "2026-10-20");
        await CreateExpenseAsync(999, "Food", "2026-10-19"); // yesterday does not count

        var a = await GetAllowanceAsync("period=Daily&date=2026-10-20");

        Assert.Equal((300m, 120m, 1, 180m, 180m), (a.Budget!.Value, a.Spent, a.DaysLeft, a.Daily!.Value, a.Total!.Value));
    }

    [Fact]
    public async Task A_paid_bill_is_not_counted_twice()
    {
        await CreateBudgetAsync("2026-10", 6000);
        var bill = await CreateBillAsync("Water", 600, "OneTime", "2026-10-25");
        var before = await GetAllowanceAsync();

        await PostAsync<BillOccurrenceResponse>($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-25" });
        var after = await GetAllowanceAsync();

        Assert.Equal((0m, 600m), (before.Spent, before.ReservedForBills));
        Assert.Equal((600m, 0m), (after.Spent, after.ReservedForBills)); // moved from "set aside" to "spent"
        Assert.Equal(before.Available, after.Available);
    }

    [Fact]
    public async Task Over_budget_suggests_nothing_more_to_spend()
    {
        await CreateBudgetAsync("2026-10", 1000);
        await CreateExpenseAsync(1200, "Food", "2026-10-05");

        var a = await GetAllowanceAsync();

        Assert.Equal(("OverBudget", -200m, 0m, 0m), (a.Status, a.Available!.Value, a.Daily!.Value, a.Total!.Value));
    }

    [Fact]
    public async Task No_budget_and_other_periods_are_handled()
    {
        var none = await GetAllowanceAsync();
        await CreateBudgetAsync("2026-09", 5000);
        await CreateBudgetAsync("2026-11", 3000);
        await PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-12", amount = 700 });
        var pastMonth = await GetAllowanceAsync("month=2026-09");
        var futureMonth = await GetAllowanceAsync("month=2026-11");
        var lastWeek = await GetAllowanceAsync("period=Weekly&date=2026-10-14");

        Assert.Equal(("NoBudget", (decimal?)null), (none.Status, none.Daily));
        Assert.Equal(("Ended", 0, 0m), (pastMonth.Status, pastMonth.DaysLeft, pastMonth.Daily!.Value));
        Assert.Equal((30, 100m, 3000m), (futureMonth.DaysLeft, futureMonth.Daily!.Value, futureMonth.Total!.Value));
        Assert.Equal(("Ended", 0), (lastWeek.Status, lastWeek.DaysLeft));
    }

    [Fact]
    public async Task Invalid_queries_are_rejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/allowance?period=Yearly")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/allowance?period=Weekly&date=2026-13-40")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync("/api/auth/preferences", new { budgetPeriod = "Hourly" })).StatusCode);
    }
}

// The last day of the month: one day left, so everything left is today's allowance.
public class AllowanceMonthEndTests() : FeatureTestBase(new DateTimeOffset(2026, 10, 31, 2, 0, 0, TimeSpan.Zero))
{
    [Fact]
    public async Task On_the_last_day_everything_left_is_todays_allowance()
    {
        await CreateBudgetAsync("2026-10", 3100);
        await CreateExpenseAsync(3000, "Food", "2026-10-10");

        var a = (await Client.GetFromJsonAsync<AllowanceResponse>("/api/allowance?month=2026-10"))!;

        Assert.Equal((1, 100m, 100m), (a.DaysLeft, a.Daily!.Value, a.Total!.Value));
    }
}

// Just before midnight UTC is already the next day in Manila (UTC+8).
public class AllowanceTimeZoneTests() : FeatureTestBase(new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero))
{
    [Fact]
    public async Task Today_follows_Manila_time_not_server_time()
    {
        await CreateBudgetAsync("2026-11", 3000);

        var a = (await Client.GetFromJsonAsync<AllowanceResponse>("/api/allowance?month=2026-11"))!;

        // 31 Oct 17:00 UTC = 1 Nov 01:00 in Manila, so November is the current month with 30 days left.
        Assert.Equal(("Available", 30), (a.Status, a.DaysLeft));
    }
}
