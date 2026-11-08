using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Funds without a target ("Envelope"), money from outside a budget, and
// spending straight from savings. Today is Tue Oct 20; the week is Oct 19 - 25.
public class SavingsFundsApiTests : FeatureTestBase
{
    private const string ThisWeek = "/api/dashboard/summary?period=Weekly&date=2026-10-20";

    private Task<BudgetResponse> WeeklyBudgetAsync(decimal amount = 1000, object[]? limits = null) =>
        PostAsync<BudgetResponse>("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount, categoryLimits = limits ?? [] });

    private async Task<DashboardSummaryResponse> SummaryAsync() =>
        (await Client.GetFromJsonAsync<DashboardSummaryResponse>(ThisWeek))!;

    [Fact]
    public async Task A_fund_needs_no_target_and_its_starting_amount_is_not_from_the_budget()
    {
        await WeeklyBudgetAsync();

        var fund = await PostAsync<SavingsGoalResponse>("/api/savings", new { name = "Envelope", startingAmount = 17500 });

        Assert.Equal(((decimal?)null, 17500m, 0m, "Active"), (fund.TargetAmount, fund.SavedAmount, fund.PercentSaved, fund.Status));
        var summary = await SummaryAsync();
        Assert.Equal((0m, 1000m), (summary.Saved, summary.Remaining!.Value));
        var history = await Client.GetFromJsonAsync<List<SavingsTransactionResponse>>($"/api/savings/{fund.Id}/transactions");
        Assert.True(Assert.Single(history!).FromOutside);
    }

    [Fact]
    public async Task Money_from_somewhere_else_does_not_touch_the_budget_but_money_from_the_budget_does()
    {
        await WeeklyBudgetAsync();
        var wallet = await PostAsync<SavingsGoalResponse>("/api/savings", new { name = "School fund" });

        await PostAsync<SavingsGoalResponse>($"/api/savings/{wallet.Id}/transactions", new { type = "Contribution", amount = 500, date = "2026-10-20", fromOutside = true });
        await PostAsync<SavingsGoalResponse>($"/api/savings/{wallet.Id}/transactions", new { type = "Contribution", amount = 200, date = "2026-10-20" });

        var summary = await SummaryAsync();
        Assert.Equal((200m, 800m), (summary.Saved, summary.Remaining!.Value));
        var saved = await Client.GetFromJsonAsync<SavingsGoalResponse>($"/api/savings/{wallet.Id}");
        Assert.Equal(700m, saved!.SavedAmount);
    }

    [Fact]
    public async Task Spending_from_savings_records_an_expense_that_does_not_use_the_budget()
    {
        await WeeklyBudgetAsync(1000, [new { category = "School", amount = 100 }]);
        var fund = await PostAsync<SavingsGoalResponse>("/api/savings", new { name = "School fund", startingAmount = 1000 });

        var after = await PostAsync<SavingsGoalResponse>($"/api/savings/{fund.Id}/transactions",
            new { type = "Withdrawal", amount = 300, date = "2026-10-20", note = "Project supplies", recordExpense = true, category = "School" });

        Assert.Equal(700m, after.SavedAmount);
        var expenses = await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses?month=2026-10");
        var expense = Assert.Single(expenses!);
        Assert.Equal(("Project supplies", "School", 300m, "School fund"), (expense.Description, expense.Category, expense.Amount, expense.PaidFrom));
        var summary = await SummaryAsync();
        var school = summary.Categories.Single(c => c.Category == "School");
        Assert.Equal((0m, 300m, 0m, 1000m, 0m), (summary.TotalSpent, summary.SpentFromSavings, summary.Saved, summary.Remaining!.Value, school.Total));
        Assert.Equal("School fund", Assert.Single(summary.RecentExpenses).PaidFrom);
        // Only the starting amount: the withdrawal is shown as the expense it paid for.
        Assert.True(Assert.Single(summary.RecentSavings).FromOutside);
    }

    [Fact]
    public async Task Deleting_an_expense_paid_from_savings_puts_the_money_back()
    {
        var fund = await PostAsync<SavingsGoalResponse>("/api/savings", new { name = "Envelope", startingAmount = 1000 });
        await PostAsync<SavingsGoalResponse>($"/api/savings/{fund.Id}/transactions",
            new { type = "Withdrawal", amount = 300, date = "2026-10-20", recordExpense = true, category = "Food" });
        var expense = (await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses?month=2026-10"))!.Single();
        Assert.Equal("Paid from Envelope", expense.Description);

        (await Client.DeleteAsync($"/api/expenses/{expense.Id}")).EnsureSuccessStatusCode();

        var saved = await Client.GetFromJsonAsync<SavingsGoalResponse>($"/api/savings/{fund.Id}");
        Assert.Equal(1000m, saved!.SavedAmount);
    }

    [Fact]
    public async Task Editing_an_expense_paid_from_savings_changes_the_withdrawal_but_not_past_the_balance()
    {
        var fund = await PostAsync<SavingsGoalResponse>("/api/savings", new { name = "Envelope", startingAmount = 500 });
        await PostAsync<SavingsGoalResponse>($"/api/savings/{fund.Id}/transactions",
            new { type = "Withdrawal", amount = 300, date = "2026-10-20", note = "Shoes", recordExpense = true, category = "Others" });
        var expense = (await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses?month=2026-10"))!.Single();

        var ok = await Client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new { amount = 450, category = "Others", description = "Shoes", expenseDate = "2026-10-19" });
        ok.EnsureSuccessStatusCode();
        Assert.Equal(50m, (await Client.GetFromJsonAsync<SavingsGoalResponse>($"/api/savings/{fund.Id}"))!.SavedAmount);

        var tooMuch = await Client.PutAsJsonAsync($"/api/expenses/{expense.Id}", new { amount = 600, category = "Others", description = "Shoes", expenseDate = "2026-10-19" });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
    }

    [Fact]
    public async Task Recording_an_expense_needs_a_category()
    {
        var fund = await PostAsync<SavingsGoalResponse>("/api/savings", new { name = "Envelope", startingAmount = 500 });

        var response = await Client.PostAsJsonAsync($"/api/savings/{fund.Id}/transactions",
            new { type = "Withdrawal", amount = 100, date = "2026-10-20", recordExpense = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(500m, (await Client.GetFromJsonAsync<SavingsGoalResponse>($"/api/savings/{fund.Id}"))!.SavedAmount);
    }

    [Fact]
    public async Task The_report_keeps_spending_from_savings_out_of_the_budget()
    {
        await CreateBudgetAsync("2026-10", 1000);
        await CreateExpenseAsync(200, "Food", "2026-10-10");
        var fund = await PostAsync<SavingsGoalResponse>("/api/savings", new { name = "Envelope", startingAmount = 1000 });
        await PostAsync<SavingsGoalResponse>($"/api/savings/{fund.Id}/transactions",
            new { type = "Withdrawal", amount = 300, date = "2026-10-12", note = "Shoes", recordExpense = true, category = "Others" });

        var report = await Client.GetFromJsonAsync<MonthlyReportResponse>("/api/reports/monthly?month=2026-10");

        Assert.Equal((200m, 800m, 300m, 0m), (report!.TotalSpent, report.Remaining!.Value, report.Savings.SpentFromSavings, report.Savings.FromBudget));
        var shoes = Assert.Single(report.Transactions, t => t.Description == "Shoes");
        Assert.Equal(("Expense", "Envelope"), (shoes.Kind, shoes.PaidFrom));
        Assert.DoesNotContain(report.Transactions, t => t.Kind == "SavingsWithdrawal");
    }
}
