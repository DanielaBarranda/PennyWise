using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Today is Tue 20 Oct 2026: 12 days left in October (today included) and
// 6 days left in the week (to Sunday 25 Oct).
public class AllowanceApiTests : FeatureTestBase
{
    private async Task<AllowanceResponse> GetAllowanceAsync(string query = "month=2026-10") =>
        (await Client.GetFromJsonAsync<AllowanceResponse>($"/api/allowance?{query}"))!;

    [Fact]
    public async Task Allowance_spreads_what_is_left_after_spending_and_unpaid_bills()
    {
        await CreateBudgetAsync("2026-10", 6000);
        await CreateExpenseAsync(1000, "Food", "2026-10-05");
        await CreateBillAsync("Water", 600, "OneTime", "2026-10-25");

        var a = await GetAllowanceAsync("month=2026-10&period=Daily");

        Assert.Equal((6000m, 1000m, 600m, 4400m), (a.Budget!.Value, a.Spent, a.ReservedForBills, a.Available!.Value));
        Assert.Equal((12, 6), (a.DaysLeft, a.DaysLeftInWeek));
        Assert.Equal(366.66m, a.Daily);   // 4400 / 12, rounded down
        Assert.Equal(2199.96m, a.Weekly); // daily x 6
        Assert.Equal(4400m, a.Monthly);
        Assert.Equal((366.66m, "Daily", "Available"), (a.Amount!.Value, a.Period, a.Status));
    }

    [Fact]
    public async Task Switching_the_view_never_changes_the_budget_or_the_total_available()
    {
        await CreateBudgetAsync("2026-10", 6000);
        await CreateExpenseAsync(1000, "Food", "2026-10-05");

        var results = new List<AllowanceResponse>();
        foreach (var period in new[] { "Daily", "Weekly", "Monthly" })
        {
            (await Client.PutAsJsonAsync("/api/auth/preferences", new { allowancePeriod = period })).EnsureSuccessStatusCode();
            results.Add(await GetAllowanceAsync()); // uses the saved preference
        }
        var budget = (await Client.GetFromJsonAsync<List<BudgetResponse>>("/api/budgets?month=2026-10"))!.Single();

        Assert.Equal(["Daily", "Weekly", "Monthly"], results.Select(r => r.Period));
        Assert.All(results, r => Assert.Equal((6000m, 5000m), (r.Budget!.Value, r.Available!.Value)));
        Assert.Equal(6000m, budget.Amount);
        Assert.Equal("Monthly", (await Client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!.AllowancePeriod);
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

        Assert.Equal(("OverBudget", -200m, 0m, 0m, 0m), (a.Status, a.Available!.Value, a.Daily!.Value, a.Weekly!.Value, a.Monthly!.Value));
    }

    [Fact]
    public async Task No_budget_and_other_months_are_handled()
    {
        var none = await GetAllowanceAsync();
        await CreateBudgetAsync("2026-09", 5000);
        await CreateBudgetAsync("2026-11", 3000);
        var past = await GetAllowanceAsync("month=2026-09");
        var future = await GetAllowanceAsync("month=2026-11&period=Weekly");

        Assert.Equal(("NoBudget", (decimal?)null), (none.Status, none.Amount));
        Assert.Equal(("MonthEnded", 0, 0m), (past.Status, past.DaysLeft, past.Daily!.Value));
        // November 2026 has 30 days and starts on a Sunday: one day in its first week.
        Assert.Equal((30, 1, 100m, 100m), (future.DaysLeft, future.DaysLeftInWeek, future.Daily!.Value, future.Amount!.Value));
    }

    [Fact]
    public async Task Invalid_period_is_rejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/allowance?period=Yearly")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync("/api/auth/preferences", new { allowancePeriod = "Hourly" })).StatusCode);
    }
}

// The last day of the month: one day left, and the week is cut at the month's end.
public class AllowanceMonthEndTests() : FeatureTestBase(new DateTimeOffset(2026, 10, 31, 2, 0, 0, TimeSpan.Zero))
{
    [Fact]
    public async Task On_the_last_day_everything_left_is_todays_allowance()
    {
        await CreateBudgetAsync("2026-10", 3100);
        await CreateExpenseAsync(3000, "Food", "2026-10-10");

        var a = (await Client.GetFromJsonAsync<AllowanceResponse>("/api/allowance?month=2026-10&period=Weekly"))!;

        // Sat 31 Oct: Sunday is 1 Nov, outside October, so the week has 1 day.
        Assert.Equal((1, 1, 100m, 100m, 100m), (a.DaysLeft, a.DaysLeftInWeek, a.Daily!.Value, a.Weekly!.Value, a.Monthly!.Value));
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
