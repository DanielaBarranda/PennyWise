using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;

namespace PennyWise.Api.Tests;

public class DeleteAccountApiTests : FeatureTestBase
{
    private static Task<HttpResponseMessage> DeleteAccountAsync(HttpClient client, string password = DefaultPassword) =>
        client.PostAsJsonAsync("/api/auth/delete-account", new { password });

    // One of everything a user can own, so the test proves none of it survives.
    private async Task<int> FillAccountAsync()
    {
        var me = (await Client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
        await CreateBudgetAsync("2026-10", 5000, ("Food", 1000));
        await CreateExpenseAsync(120, "Food", "2026-10-18");
        var goal = await CreateGoalAsync("Laptop", 30000);
        await AddSavingsAsync(goal.Id, "Contribution", 500);
        var bill = await CreateBillAsync("Internet", 1299, "Monthly", "2026-10-20");
        (await Client.PostAsJsonAsync($"/api/bills/{bill.Id}/payments", new { dueDate = "2026-10-20" })).EnsureSuccessStatusCode();
        var photo = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1, 2]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await Client.PutAsync("/api/auth/avatar", photo)).EnsureSuccessStatusCode();
        return me.Id;
    }

    private async Task<Dictionary<string, int>> RowsOwnedByAsync(int userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PennyWiseDbContext>();
        return new()
        {
            ["users"] = await db.Users.CountAsync(u => u.Id == userId),
            ["budgets"] = await db.Budgets.CountAsync(b => b.UserId == userId),
            ["categoryLimits"] = await db.Set<CategoryLimit>().CountAsync(l => db.Budgets.Any(b => b.Id == l.BudgetId && b.UserId == userId)),
            ["expenses"] = await db.Expenses.CountAsync(e => e.UserId == userId),
            ["goals"] = await db.SavingsGoals.CountAsync(g => g.UserId == userId),
            ["savingsTransactions"] = await db.SavingsTransactions.CountAsync(t => db.SavingsGoals.Any(g => g.Id == t.GoalId && g.UserId == userId)),
            ["bills"] = await db.Bills.CountAsync(b => b.UserId == userId),
            ["billPayments"] = await db.BillPayments.CountAsync(p => db.Bills.Any(b => b.Id == p.BillId && b.UserId == userId)),
            ["avatars"] = await db.UserAvatars.CountAsync(a => a.UserId == userId),
        };
    }

    [Fact]
    public async Task Wrong_password_deletes_nothing()
    {
        var userId = await FillAccountAsync();

        var response = await DeleteAccountAsync(Client, "Wrong2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal("Password is incorrect.", problem!.Errors["Password"].Single());
        Assert.All(await RowsOwnedByAsync(userId), row => Assert.True(row.Value > 0, $"{row.Key} should still exist"));
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Missing_password_is_rejected()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/delete-account", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_removes_the_account_and_all_its_data()
    {
        var userId = await FillAccountAsync();

        var response = await DeleteAccountAsync(Client);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.All(await RowsOwnedByAsync(userId), row => Assert.Equal(0, row.Value));
    }

    [Fact]
    public async Task Deleting_signs_out_this_browser_and_every_other_device()
    {
        var otherDevice = NewClient();
        (await LoginAsync(otherDevice, DefaultEmail)).EnsureSuccessStatusCode();

        await DeleteAccountAsync(Client);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/expenses")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), DefaultEmail)).StatusCode);
    }

    [Fact]
    public async Task Other_users_keep_their_data()
    {
        await FillAccountAsync();
        var other = await NewUserAsync("other@pennywise.test");
        await CreateExpenseAsync(other, 75, "Transportation", "2026-10-19");

        await DeleteAccountAsync(Client);

        var expenses = await other.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses");
        Assert.Single(expenses!);
    }

    [Fact]
    public async Task The_email_can_sign_up_again_as_a_new_empty_account()
    {
        await FillAccountAsync();
        await DeleteAccountAsync(Client);

        var fresh = NewClient();
        (await RegisterAsync(fresh, DefaultEmail)).EnsureSuccessStatusCode();

        Assert.Empty((await fresh.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses"))!);
        Assert.Empty((await fresh.GetFromJsonAsync<List<SavingsGoalResponse>>("/api/savings"))!);
        Assert.Null((await fresh.GetFromJsonAsync<UserResponse>("/api/auth/me"))!.AvatarVersion);
    }

    [Fact]
    public async Task Needs_sign_in_and_the_client_header()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await DeleteAccountAsync(NewClient())).StatusCode);

        var withoutHeader = Factory.CreateClient();
        var response = await withoutHeader.PostAsJsonAsync("/api/auth/delete-account", new { password = DefaultPassword });
        Assert.NotEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/auth/me")).StatusCode);
    }
}
