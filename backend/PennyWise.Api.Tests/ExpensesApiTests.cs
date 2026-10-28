using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class ExpensesApiTests : ApiTestBase
{
    [Fact]
    public async Task Health_reports_api_and_database_ok()
    {
        var response = await Client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"database\":\"ok\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_then_get_returns_saved_expense_with_trimmed_description()
    {
        var created = await CreateExpenseAsync(85.50m, "Food", "2026-10-05", "  Lunch at canteen  ");

        var fetched = await Client.GetFromJsonAsync<ExpenseResponse>($"/api/expenses/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(85.50m, fetched.Amount);
        Assert.Equal("Food", fetched.Category);
        Assert.Equal("Lunch at canteen", fetched.Description);
        Assert.Equal(new DateOnly(2026, 10, 5), fetched.ExpenseDate);
    }

    [Theory]
    [InlineData("""{"amount":-5,"category":"Food","description":"x","expenseDate":"2026-10-03"}""", "Amount")]
    [InlineData("""{"amount":0,"category":"Food","description":"x","expenseDate":"2026-10-03"}""", "Amount")]
    [InlineData("""{"amount":1.234,"category":"Food","description":"x","expenseDate":"2026-10-03"}""", "Amount")]
    [InlineData("""{"amount":10000000.01,"category":"Food","description":"x","expenseDate":"2026-10-03"}""", "Amount")]
    [InlineData("""{"category":"Food","description":"x","expenseDate":"2026-10-03"}""", "Amount")]
    [InlineData("""{"amount":5,"category":"Shopping","description":"x","expenseDate":"2026-10-03"}""", "Category")]
    [InlineData("""{"amount":5,"category":"food","description":"x","expenseDate":"2026-10-03"}""", "Category")]
    [InlineData("""{"amount":5,"category":"Food","description":"   ","expenseDate":"2026-10-03"}""", "Description")]
    [InlineData("""{"amount":5,"category":"Food","description":"x"}""", "ExpenseDate")]
    [InlineData("""{"amount":5,"category":"Food","description":"x","expenseDate":"1999-12-31"}""", "ExpenseDate")]
    [InlineData("""{"amount":"abc","category":"Food","description":"x","expenseDate":"2026-10-03"}""", "$.amount")]
    public async Task Invalid_expense_is_rejected_with_field_error(string json, string expectedField)
    {
        var response = await Client.PostAsync("/api/expenses", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(expectedField, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Invalid_expense_is_not_saved()
    {
        await Client.PostAsJsonAsync("/api/expenses", new { amount = -5, category = "Food", description = "x", expenseDate = "2026-10-03" });

        var all = await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses");
        Assert.Empty(all!);
    }

    [Fact]
    public async Task Month_filter_includes_first_and_last_day_only()
    {
        await CreateExpenseAsync(1, "Food", "2026-09-30");
        await CreateExpenseAsync(2, "Food", "2026-10-01");
        await CreateExpenseAsync(3, "Food", "2026-10-31");
        await CreateExpenseAsync(4, "Food", "2026-11-01");

        var october = await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses?month=2026-10");

        Assert.Equal([3m, 2m], october!.Select(e => e.Amount)); // newest date first
    }

    [Fact]
    public async Task Category_filter_returns_only_that_category()
    {
        await CreateExpenseAsync(10, "Food", "2026-10-01");
        await CreateExpenseAsync(20, "Bills", "2026-10-02");

        var bills = await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses?month=2026-10&category=Bills");

        Assert.Single(bills!);
        Assert.Equal("Bills", bills![0].Category);
    }

    [Theory]
    [InlineData("/api/expenses?month=October")]
    [InlineData("/api/expenses?month=2026-13")]
    [InlineData("/api/expenses?category=Shopping")]
    [InlineData("/api/dashboard/summary?month=2026-1")]
    public async Task Invalid_query_filters_return_400(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_the_expense()
    {
        var created = await CreateExpenseAsync(100, "Food", "2026-10-05");

        var response = await Client.PutAsJsonAsync($"/api/expenses/{created.Id}",
            new { amount = 250.25m, category = "Entertainment", description = "Movie", expenseDate = "2026-10-06" });
        var updated = await response.Content.ReadFromJsonAsync<ExpenseResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(250.25m, updated!.Amount);
        Assert.Equal("Entertainment", updated.Category);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
    }

    [Fact]
    public async Task Delete_removes_the_expense()
    {
        var created = await CreateExpenseAsync(100, "Food", "2026-10-05");

        var delete = await Client.DeleteAsync($"/api/expenses/{created.Id}");
        var getAfter = await Client.GetAsync($"/api/expenses/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
    }

    [Fact]
    public async Task Missing_ids_return_404()
    {
        var body = new { amount = 1, category = "Food", description = "x", expenseDate = "2026-10-05" };

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/expenses/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PutAsJsonAsync("/api/expenses/999", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync("/api/expenses/999")).StatusCode);
    }

    [Fact]
    public async Task Sql_injection_text_is_stored_as_plain_text()
    {
        const string attack = "x'); DROP TABLE Expenses;--";
        var created = await CreateExpenseAsync(1, "Others", "2026-10-05", attack);

        var all = await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses");

        Assert.Equal(attack, all!.Single(e => e.Id == created.Id).Description);
    }

    [Fact]
    public async Task Data_survives_an_api_restart()
    {
        await CreateBudgetAsync("2026-10", 5000);
        await CreateExpenseAsync(1234.56m, "School", "2026-10-15", "Thesis printing");

        await RestartApiAsync();

        var summary = await GetSummaryAsync("2026-10");
        Assert.True(File.Exists(DatabasePath));
        Assert.Equal(5000m, summary.Budget!.Amount);
        Assert.Equal(1234.56m, summary.TotalSpent);
        Assert.Equal("Thesis printing", summary.RecentExpenses.Single().Description);
    }
}
