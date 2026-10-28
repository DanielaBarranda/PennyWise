using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Two accounts must never see or change each other's financial records,
// whatever ids, URLs or request bodies they send.
public class IsolationApiTests : ApiTestBase
{
    private const string Month = "2026-10";

    [Fact]
    public async Task Each_user_sees_only_their_own_expenses_budget_and_dashboard()
    {
        await CreateBudgetAsync(Month, 5000, ("Food", 2000));
        await CreateExpenseAsync(1200, "Food", "2026-10-05", "Alice groceries");
        var bob = await NewUserAsync("bob@pennywise.test");
        await CreateBudgetAsync(bob, Month, 9000); // same month is fine: budgets are per user
        await CreateExpenseAsync(bob, 300, "Bills", "2026-10-06", "Bob internet");

        var aliceList = await Client.GetFromJsonAsync<List<ExpenseResponse>>($"/api/expenses?month={Month}");
        var bobList = await bob.GetFromJsonAsync<List<ExpenseResponse>>($"/api/expenses?month={Month}");
        var aliceSummary = await GetSummaryAsync(Month);
        var bobSummary = await GetSummaryAsync(bob, Month);
        var bobBudgets = await bob.GetFromJsonAsync<List<BudgetResponse>>("/api/budgets");

        Assert.Equal(["Alice groceries"], aliceList!.Select(e => e.Description));
        Assert.Equal(["Bob internet"], bobList!.Select(e => e.Description));
        Assert.Equal((5000m, 1200m), (aliceSummary.Budget!.Amount, aliceSummary.TotalSpent));
        Assert.Equal((9000m, 300m), (bobSummary.Budget!.Amount, bobSummary.TotalSpent));
        Assert.Equal([9000m], bobBudgets!.Select(b => b.Amount));
    }

    [Fact]
    public async Task Another_users_expense_cannot_be_read_changed_or_deleted_by_id()
    {
        var alices = await CreateExpenseAsync(1200, "Food", "2026-10-05", "Alice groceries");
        var bob = await NewUserAsync("bob@pennywise.test");
        var change = new { amount = 1, category = "Others", description = "hacked", expenseDate = "2026-10-05" };

        var read = await bob.GetAsync($"/api/expenses/{alices.Id}");
        var update = await bob.PutAsJsonAsync($"/api/expenses/{alices.Id}", change);
        var delete = await bob.DeleteAsync($"/api/expenses/{alices.Id}");
        var stillThere = await Client.GetFromJsonAsync<ExpenseResponse>($"/api/expenses/{alices.Id}");

        // 404, not 403: Bob cannot even learn that the id exists.
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal((1200m, "Alice groceries"), (stillThere!.Amount, stillThere.Description));
    }

    [Fact]
    public async Task Another_users_budget_cannot_be_read_or_changed_by_id()
    {
        var alices = await CreateBudgetAsync(Month, 5000);
        var bob = await NewUserAsync("bob@pennywise.test");

        var read = await bob.GetAsync($"/api/budgets/{alices.Id}");
        var update = await bob.PutAsJsonAsync($"/api/budgets/{alices.Id}", new { month = Month, amount = 1 });
        var stillThere = await Client.GetFromJsonAsync<BudgetResponse>($"/api/budgets/{alices.Id}");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(5000m, stillThere!.Amount);
    }

    [Fact]
    public async Task A_user_id_in_the_request_body_is_ignored()
    {
        var alice = await Client.GetFromJsonAsync<UserResponse>("/api/auth/me");
        var bob = await NewUserAsync("bob@pennywise.test");

        await bob.PostAsJsonAsync("/api/expenses",
            new { userId = alice!.Id, amount = 50, category = "Food", description = "Bob tries to file under Alice", expenseDate = "2026-10-07" });

        Assert.Empty((await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses"))!);
        Assert.Single((await bob.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses"))!);
    }

    [Fact]
    public async Task Signing_out_hides_the_data_again()
    {
        await CreateExpenseAsync(1200, "Food", "2026-10-05");

        await Client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/expenses")).StatusCode);
    }
}
