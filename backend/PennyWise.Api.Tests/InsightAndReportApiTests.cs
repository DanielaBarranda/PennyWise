using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class InsightApiTests : FeatureTestBase
{
    private async Task<List<InsightResponse>> GetInsightsAsync(string month = "2026-10") =>
        (await Client.GetFromJsonAsync<List<InsightResponse>>($"/api/insights?month={month}"))!;

    [Fact]
    public async Task A_new_account_has_no_insights()
    {
        Assert.Empty(await GetInsightsAsync());
    }

    [Fact]
    public async Task Category_usage_is_calculated_from_real_spending_and_days_left()
    {
        await CreateBudgetAsync("2026-10", 5000, ("Food", 2000));
        await CreateExpenseAsync(1600, "Food", "2026-10-08");

        var insight = Assert.Single(await GetInsightsAsync());

        Assert.Equal("You've used 80% of your Food limit, with 12 days left in the month. ₱400.00 remains.", insight.Message);
        Assert.Equal(("Warning", "Fact"), (insight.Severity, insight.Kind));
    }

    [Fact]
    public async Task A_category_over_its_limit_gets_one_insight_not_two()
    {
        await CreateBudgetAsync("2026-10", 5000, ("Food", 1000));
        await CreateExpenseAsync(1250, "Food", "2026-10-08");

        var food = (await GetInsightsAsync()).Where(i => i.Key.Contains("Food")).ToList();

        Assert.Equal("Food spending is ₱250.00 over its ₱1,000.00 limit.", Assert.Single(food).Message);
    }

    [Fact]
    public async Task Past_months_never_mention_days_left()
    {
        await CreateBudgetAsync("2026-09", 5000, ("Food", 2000));
        await CreateExpenseAsync(1700, "Food", "2026-09-08");

        var insight = Assert.Single(await GetInsightsAsync("2026-09"));

        Assert.DoesNotContain("days left", insight.Message);
    }

    [Fact]
    public async Task Month_comparison_uses_the_same_days_in_both_months()
    {
        await CreateExpenseAsync(1000, "Transportation", "2026-09-10");
        await CreateExpenseAsync(5000, "Transportation", "2026-09-25"); // after Sep 20: outside the compared days
        await CreateExpenseAsync(1200, "Transportation", "2026-10-12");

        var insight = (await GetInsightsAsync()).Single(i => i.Key.StartsWith("cmp-"));

        Assert.Equal("Transportation spending is up 20% compared with the same days last month " +
                     "(₱1,200.00 in the first 20 days of the month, against ₱1,000.00).", insight.Message);
    }

    [Fact]
    public async Task Urgent_insights_come_first_and_facts_are_separated_from_suggestions()
    {
        await CreateExpenseAsync(300, "Food", "2026-10-05");          // spending without a budget -> suggestion
        await CreateBillAsync("Water", 450, "OneTime", "2026-10-18");  // overdue -> critical
        await CreateBillAsync("Internet", 1299, "OneTime", "2026-10-23"); // due in 3 days
        var goal = await CreateGoalAsync("Fund", 500);
        await AddSavingsAsync(goal.Id, "Contribution", 500);          // reached -> success

        var insights = await GetInsightsAsync();

        Assert.Equal(["Critical", "Warning", "Success", "Info"], insights.Select(i => i.Severity));
        Assert.Equal("Your Water bill (₱450.00) was due on Oct 18.", insights[0].Message);
        Assert.False(insights[0].Dismissible);
        Assert.Equal("Your Internet bill (₱1,299.00) is due in 3 days.", insights[1].Message);
        Assert.Equal("You've reached your Fund goal of ₱500.00.", insights[2].Message);
        Assert.Equal("Suggestion", insights[3].Kind);
    }
}

public class ReportApiTests : FeatureTestBase
{
    private async Task<MonthlyReportResponse> GetReportAsync(HttpClient? client = null) =>
        (await (client ?? Client).GetFromJsonAsync<MonthlyReportResponse>("/api/reports/monthly?month=2026-10"))!;

    [Fact]
    public async Task Report_uses_saved_records_and_counts_a_bill_payment_once()
    {
        await CreateBudgetAsync("2026-10", 10000, ("Food", 3000));
        await CreateExpenseAsync(2500, "Food", "2026-10-03");
        await CreateExpenseAsync(2000, "Food", "2026-09-15");
        var bill = await CreateBillAsync("Electricity", 1500, "OneTime", "2026-10-18");
        await PostAsync<BillOccurrenceResponse>($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-18", paidOn = "2026-10-18" });
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 3000, "2026-10-10");
        await AddSavingsAsync(goal.Id, "Withdrawal", 500, "2026-10-15");

        var r = await GetReportAsync();

        Assert.Equal((4000m, 2, 40.0m, 6000m), (r.TotalSpent, r.ExpenseCount, r.BudgetUsedPercent!.Value, r.Remaining!.Value)); // 2500 + 1500 bill
        Assert.Equal((2000m, 100.0m), (r.PreviousTotalSpent, r.ChangeFromPreviousPercent!.Value));
        Assert.Equal((1, 1500m, true), (r.Bills.Paid, r.Bills.PaidAmount, r.Bills.Items.Single().RecordedAsExpense));
        Assert.Equal((3000m, 500m, 2500m), (r.Savings.Contributions, r.Savings.Withdrawals, r.Savings.Net));
        Assert.Equal(2500m, r.Savings.Goals.Single().SavedAtMonthEnd);
        Assert.Contains(r.Notes, n => n.Contains("Income is not recorded"));
    }

    [Fact]
    public async Task The_csv_export_matches_the_report()
    {
        await CreateBudgetAsync("2026-10", 10000);
        await CreateExpenseAsync(1234.5m, "School", "2026-10-03");
        var goal = await CreateGoalAsync("=HYPERLINK(\"http://evil\")", 1000);
        await AddSavingsAsync(goal.Id, "Contribution", 100, "2026-10-04");

        var r = await GetReportAsync();
        var response = await Client.GetAsync("/api/reports/monthly.csv?month=2026-10");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("pennywise-report-2026-10.csv", response.Content.Headers.ContentDisposition!.ToString());
        Assert.Contains($"Total spent,{r.TotalSpent:0.00}", csv);
        Assert.Contains($"Budget used (%),{r.BudgetUsedPercent:0.00}", csv);
        Assert.Contains("School,1234.50,100.00", csv);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", csv); // neutralised formula
    }

    [Fact]
    public async Task An_empty_month_produces_an_empty_report_not_an_error()
    {
        var r = await GetReportAsync();

        Assert.Equal((0m, 0, (decimal?)null, (decimal?)null), (r.TotalSpent, r.ExpenseCount, r.Budget, r.ChangeFromPreviousPercent));
        Assert.Empty(r.Categories);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/reports/monthly.csv?month=2026-10")).StatusCode);
    }

    [Fact]
    public async Task Reports_only_include_the_signed_in_users_records()
    {
        await CreateExpenseAsync(999, "Food", "2026-10-03");
        var bob = await NewUserAsync("bob@pennywise.test");

        var bobs = await GetReportAsync(bob);

        Assert.Equal(0m, bobs.TotalSpent);
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync("/api/reports/monthly.csv")).StatusCode);
    }
}
