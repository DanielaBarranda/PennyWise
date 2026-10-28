using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;

namespace PennyWise.Api.Tests;

// Records created before accounts existed (UserId = null) must be preserved,
// hidden from everyone, and handed only to the configured owner email.
public class LegacyDataTests() : ApiTestBase(new Dictionary<string, string> { ["PennyWise:LegacyOwnerEmail"] = "Daniela@PennyWise.test" })
{
    public override async Task InitializeAsync()
    {
        Client = NewClient();
        await Client.GetAsync("/api/health"); // starts the app, which applies the migrations
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PennyWiseDbContext>();
        var now = DateTime.UtcNow;
        db.Budgets.Add(new Budget
        {
            Month = "2026-10", Amount = 4000, CreatedAt = now, UpdatedAt = now,
            CategoryLimits = [new CategoryLimit { Category = Category.Food, Amount = 1500 }],
        });
        db.Expenses.Add(new Expense { Amount = 250, Category = Category.Food, Description = "Old lunch", ExpenseDate = new DateOnly(2026, 10, 2), CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Other_accounts_never_receive_the_old_records()
    {
        var stranger = await NewUserAsync("stranger@pennywise.test");

        Assert.Empty((await stranger.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses"))!);
        Assert.Empty((await stranger.GetFromJsonAsync<List<BudgetResponse>>("/api/budgets"))!);
    }

    [Fact]
    public async Task The_configured_owner_receives_the_old_records_when_registering()
    {
        await NewUserAsync("stranger@pennywise.test"); // registering first changes nothing
        var owner = await NewUserAsync("  daniela@pennywise.test"); // case and spaces do not matter

        var summary = await GetSummaryAsync(owner, "2026-10");

        Assert.Equal(4000m, summary.Budget!.Amount);
        Assert.Equal(1500m, summary.Budget.CategoryLimits.Single().Amount);
        Assert.Equal(250m, summary.TotalSpent);
    }

    [Fact]
    public async Task The_owners_own_budget_for_a_month_is_kept_and_the_old_one_stays_preserved()
    {
        // The owner registered before the setting existed and already budgeted October.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PennyWiseDbContext>();
        var owner = await NewUserAsync("daniela@pennywise.test");
        // (Registration already claimed the old budget; recreate the conflict explicitly.)
        var mine = db.Budgets.Single();
        mine.UserId = null;
        db.Budgets.Add(new Budget { UserId = db.Users.Single().Id, Month = "2026-10", Amount = 7000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        (await owner.PostAsync("/api/auth/logout", null)).EnsureSuccessStatusCode();
        (await LoginAsync(owner, "daniela@pennywise.test")).EnsureSuccessStatusCode();
        var summary = await GetSummaryAsync(owner, "2026-10");

        Assert.Equal(7000m, summary.Budget!.Amount);
        Assert.Equal(2, db.Budgets.Count()); // the older budget still exists, unowned
    }
}
