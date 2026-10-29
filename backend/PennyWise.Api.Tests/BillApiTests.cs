using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class BillApiTests : FeatureTestBase
{
    [Fact]
    public async Task Monthly_bill_on_the_31st_falls_on_the_last_day_of_short_months()
    {
        await CreateBillAsync("Rent", 8000, "Monthly", "2026-10-31");

        var dates = (await OccurrencesAsync("2026-10-01", "2027-03-31")).Select(o => o.DueDate.ToString("yyyy-MM-dd"));

        Assert.Equal(["2026-10-31", "2026-11-30", "2026-12-31", "2027-01-31", "2027-02-28", "2027-03-31"], dates);
    }

    [Fact]
    public async Task Due_dates_before_the_bill_was_added_are_not_shown_as_overdue()
    {
        // First due in January, but added to PennyWise today (Oct 20).
        await CreateBillAsync("Internet", 1299, "Monthly", "2026-01-15");

        var occurrences = await OccurrencesAsync("2026-01-01", "2026-12-31");

        Assert.Equal(["2026-11-15", "2026-12-15"], occurrences.Select(o => o.DueDate.ToString("yyyy-MM-dd")));
        Assert.DoesNotContain(occurrences, o => o.Status == "Overdue");
    }

    [Fact]
    public async Task Statuses_follow_the_due_date()
    {
        await CreateBillAsync("Old fee", 100, "OneTime", "2026-10-15");   // 5 days ago
        await CreateBillAsync("Water", 450, "OneTime", "2026-10-23");     // in 3 days
        await CreateBillAsync("Insurance", 2000, "OneTime", "2026-11-15"); // in 26 days

        var statuses = (await OccurrencesAsync("2026-10-01", "2026-11-30")).Select(o => (o.Name, o.Status, o.DaysUntilDue));

        Assert.Equal([("Old fee", "Overdue", -5), ("Water", "DueSoon", 3), ("Insurance", "Upcoming", 26)], statuses);
    }

    [Fact]
    public async Task Weekly_bills_repeat_every_seven_days_until_the_end_date()
    {
        await CreateBillAsync("Allowance for sibling", 500, "Weekly", "2026-10-21", endDate: "2026-11-11");

        var dates = (await OccurrencesAsync("2026-10-01", "2026-12-31")).Select(o => o.DueDate.ToString("MM-dd"));

        Assert.Equal(["10-21", "10-28", "11-04", "11-11"], dates);
    }

    [Fact]
    public async Task Paying_a_bill_creates_exactly_one_linked_expense()
    {
        var bill = await CreateBillAsync("Electricity", 2350.75m, "Monthly", "2026-10-25");

        var paid = await PostAsync<BillOccurrenceResponse>($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-25" });
        var again = await Client.PostAsJsonAsync($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-25" });
        var expenses = await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses?month=2026-10");
        var summary = await GetSummaryAsync("2026-10");

        Assert.Equal(("Paid", Today), (paid.Status, paid.PaidOn!.Value));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var expense = Assert.Single(expenses!);
        Assert.Equal((2350.75m, "Bills", paid.ExpenseId), (expense.Amount, expense.Category, (int?)expense.Id));
        Assert.Equal(2350.75m, summary.TotalSpent);
    }

    [Fact]
    public async Task A_payment_can_be_recorded_without_creating_an_expense()
    {
        var bill = await CreateBillAsync("Phone plan", 999, "Monthly", "2026-10-22");

        var paid = await PostAsync<BillOccurrenceResponse>($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-22", recordExpense = false });

        Assert.Null(paid.ExpenseId);
        Assert.Empty((await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses"))!);
    }

    [Fact]
    public async Task Undoing_a_payment_removes_its_expense_too()
    {
        var bill = await CreateBillAsync("Water", 450, "Monthly", "2026-10-22");
        await PostAsync<BillOccurrenceResponse>($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-22" });

        var undo = await Client.DeleteAsync($"/api/bills/{bill.Id}/payments/2026-10-22");

        Assert.Equal(HttpStatusCode.NoContent, undo.StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses"))!);
        Assert.Equal("DueSoon", (await OccurrencesAsync("2026-10-22", "2026-10-22")).Single().Status);
    }

    [Fact]
    public async Task Paying_a_date_that_is_not_a_due_date_is_rejected()
    {
        var bill = await CreateBillAsync("Rent", 8000, "Monthly", "2026-10-31");
        var response = await Client.PostAsJsonAsync($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-30" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Archived_bills_stop_appearing_in_upcoming_dates()
    {
        var bill = await CreateBillAsync("Gym", 1500, "Monthly", "2026-10-28");
        await Client.PostAsync($"/api/bills/{bill.Id}/archive", null);

        Assert.Empty(await OccurrencesAsync("2026-10-01", "2026-12-31"));
        Assert.True((await Client.GetFromJsonAsync<List<BillResponse>>("/api/bills?includeArchived=true"))!.Single().Archived);
    }

    [Theory]
    [InlineData("OneTime", "2026-10-25", "2026-12-31", "EndDate")]
    [InlineData("Monthly", "2026-10-25", "2026-10-01", "EndDate")]
    public async Task Invalid_end_dates_are_rejected(string frequency, string first, string end, string field)
    {
        var response = await Client.PostAsJsonAsync("/api/bills",
            new { name = "X", amount = 10, category = "Bills", frequency, firstDueDate = first, endDate = end });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Other_users_cannot_see_or_pay_a_bill()
    {
        var bill = await CreateBillAsync("Private bill", 100, "Monthly", "2026-10-25");
        var bob = await NewUserAsync("bob@pennywise.test");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/bills/{bill.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsJsonAsync($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-25" })).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<List<BillOccurrenceResponse>>("/api/bills/occurrences?from=2026-10-01&to=2026-12-31"))!);
    }
}
