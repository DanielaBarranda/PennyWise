using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;

namespace PennyWise.Api.Tests;

// Daily, weekly and monthly budgets each stand on their own: nothing is ever
// converted from one period to another. Today is Tue 20 Oct 2026; its week is
// Mon 19 Oct to Sun 25 Oct.
public class BudgetPeriodApiTests : FeatureTestBase
{
    private Task<BudgetResponse> CreateAsync(string period, string start, decimal amount, params object[] categoryLimits) =>
        PostAsync<BudgetResponse>("/api/budgets", new { period, start, amount, categoryLimits });

    private async Task<DashboardSummaryResponse> SummaryAsync(string query) =>
        (await Client.GetFromJsonAsync<DashboardSummaryResponse>($"/api/dashboard/summary?{query}"))!;

    [Fact]
    public async Task A_weekly_budget_is_saved_exactly_as_typed()
    {
        var budget = await CreateAsync("Weekly", "2026-10-19", 1000);

        Assert.Equal(("Weekly", new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), 1000m),
            (budget.Period, budget.Start, budget.End, budget.Amount));
        // No monthly budget appears from it.
        Assert.Null((await SummaryAsync("month=2026-10")).Budget);
    }

    [Theory]
    [InlineData("Weekly", "2026-10-20", "A weekly budget starts on a Monday.")]
    [InlineData("Monthly", "2026-10-02", "A monthly budget starts on the 1st.")]
    [InlineData("Weekly", null, "Choose the week.")]
    [InlineData("Daily", null, "Choose the day.")]
    public async Task Start_dates_must_match_the_period(string period, string? start, string message)
    {
        var response = await Client.PostAsJsonAsync("/api/budgets", new { period, start, amount = 500 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(message, problem!.Errors.SelectMany(e => e.Value));
    }

    [Fact]
    public async Task Each_period_has_at_most_one_budget_but_periods_can_overlap()
    {
        await CreateAsync("Weekly", "2026-10-19", 1000);
        await CreateAsync("Weekly", "2026-10-26", 1200);       // another week, same month
        await CreateBudgetAsync("2026-10", 5000);              // a month can have its own budget too
        await CreateAsync("Daily", "2026-10-20", 200);

        var duplicate = await Client.PostAsJsonAsync("/api/budgets", new { period = "Weekly", start = "2026-10-19", amount = 900 });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(4, (await Client.GetFromJsonAsync<List<BudgetResponse>>("/api/budgets?month=2026-10"))!.Count);
    }

    [Fact]
    public async Task The_weekly_dashboard_shows_only_that_week()
    {
        await CreateAsync("Weekly", "2026-10-19", 1000, new { category = "Food", amount = 300 });
        await CreateExpenseAsync(500, "Food", "2026-10-18");   // the Sunday before
        await CreateExpenseAsync(250, "Food", "2026-10-21");
        await CreateExpenseAsync(80, "Transportation", "2026-10-25");

        var week = await SummaryAsync("period=Weekly&date=2026-10-22");

        Assert.Equal(("Weekly", new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)), (week.Period, week.Start, week.End));
        Assert.Equal((1000m, 330m, 670m, 2), (week.Budget!.Amount, week.TotalSpent, week.Remaining!.Value, week.ExpenseCount));
        Assert.Equal(("Warning", 250m), (week.Categories.Single(c => c.Category == "Food").Status, week.Categories.Single(c => c.Category == "Food").Total));
    }

    [Fact]
    public async Task A_week_that_crosses_into_the_next_month_counts_both_months_days()
    {
        await CreateAsync("Weekly", "2026-10-26", 1000);
        await CreateExpenseAsync(100, "Food", "2026-10-30");
        await CreateExpenseAsync(200, "Food", "2026-11-01"); // Sunday, still the same week

        var week = await SummaryAsync("period=Weekly&date=2026-11-01");

        Assert.Equal((new DateOnly(2026, 10, 26), 300m), (week.Start, week.TotalSpent));
    }

    [Fact]
    public async Task The_daily_dashboard_shows_only_that_day()
    {
        await CreateAsync("Daily", "2026-10-20", 300);
        await CreateExpenseAsync(120, "Food", "2026-10-20");
        await CreateExpenseAsync(999, "Food", "2026-10-19");

        var day = await SummaryAsync("period=Daily&date=2026-10-20");

        Assert.Equal((300m, 120m, 180m), (day.Budget!.Amount, day.TotalSpent, day.Remaining!.Value));
    }

    [Fact]
    public async Task Removing_a_budget_leaves_the_period_without_one_and_keeps_expenses()
    {
        var budget = await CreateAsync("Weekly", "2026-10-19", 1000);
        await CreateExpenseAsync(100, "Food", "2026-10-20");

        var response = await Client.DeleteAsync($"/api/budgets/{budget.Id}");
        var week = await SummaryAsync("period=Weekly&date=2026-10-20");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(("NoBudget", (BudgetResponse?)null, 100m), (week.Status, week.Budget, week.TotalSpent));
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/budgets/{budget.Id}")).StatusCode);
    }

    [Fact]
    public async Task Someone_elses_budget_cannot_be_removed()
    {
        var mine = await CreateAsync("Weekly", "2026-10-19", 1000);
        var other = await NewUserAsync("other@pennywise.test");

        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/budgets/{mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/budgets/{mine.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_period_budget_can_be_found_by_any_date_inside_it()
    {
        var budget = await CreateAsync("Weekly", "2026-10-19", 1000);

        var found = await Client.GetFromJsonAsync<List<BudgetResponse>>("/api/budgets?period=Weekly&date=2026-10-24");
        var empty = await Client.GetFromJsonAsync<List<BudgetResponse>>("/api/budgets?period=Weekly&date=2026-10-26");

        Assert.Equal(budget.Id, found!.Single().Id);
        Assert.Empty(empty!);
    }

    [Fact]
    public async Task Weekly_insights_talk_about_the_week()
    {
        await CreateAsync("Weekly", "2026-10-19", 500);
        await CreateExpenseAsync(450, "Food", "2026-10-20");

        var insights = await Client.GetFromJsonAsync<List<InsightResponse>>("/api/insights?period=Weekly&date=2026-10-20");

        var budget = insights!.Single(i => i.Title == "Budget almost used");
        Assert.Contains("your budget for the week of Oct 19, with 6 days left in the week", budget.Message);
    }

    [Fact]
    public async Task The_monthly_report_lists_weekly_budgets_without_adding_them_up()
    {
        await CreateAsync("Weekly", "2026-10-19", 1000);
        await CreateAsync("Weekly", "2026-10-26", 800);
        await CreateExpenseAsync(300, "Food", "2026-10-20");
        await CreateExpenseAsync(50, "Food", "2026-11-01");   // in the second week, but November

        var report = (await Client.GetFromJsonAsync<MonthlyReportResponse>("/api/reports/monthly?month=2026-10"))!;

        Assert.Null(report.Budget);
        Assert.Equal([(1000m, 300m), (800m, 50m)], report.PeriodBudgets.Select(b => (b.Amount, b.Spent)));
        Assert.Contains(report.Notes, n => n.Contains("not added up into a monthly budget"));
    }

    [Fact]
    public async Task The_budget_period_preference_is_saved()
    {
        var response = await Client.PutAsJsonAsync("/api/auth/preferences", new { budgetPeriod = "Weekly" });
        var me = await Client.GetFromJsonAsync<UserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Weekly", me!.BudgetPeriod);
    }
}

// Budgets saved before periods existed were all monthly; upgrading must keep them.
public class BudgetPeriodMigrationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"pennywise-migration-{Guid.NewGuid():N}.db");

    [Fact]
    public void Existing_monthly_budgets_get_their_start_date()
    {
        var options = new DbContextOptionsBuilder<PennyWiseDbContext>().UseSqlite($"Data Source={_path}").Options;
        using (var db = new PennyWiseDbContext(options))
        {
            db.GetService<IMigrator>().Migrate("20261029063500_AddUserAvatars");
            db.Database.ExecuteSqlRaw(
                "INSERT INTO Users (Id, Name, Email, NormalizedEmail, PasswordHash, SecurityStamp, FailedLoginCount, CreatedAt) " +
                "VALUES (1, 'Ana', 'ana@pennywise.test', 'ANA@PENNYWISE.TEST', 'x', 'stamp', 0, '2026-10-01 00:00:00');");
            db.Database.ExecuteSqlRaw(
                "INSERT INTO Budgets (UserId, Month, Amount, CreatedAt, UpdatedAt) VALUES " +
                "(1, '2026-09', '4000.0', '2026-09-01 00:00:00', '2026-09-01 00:00:00'), " +
                "(1, '2026-10', '4.43', '2026-10-01 00:00:00', '2026-10-01 00:00:00');");

            db.GetService<IMigrator>().Migrate();
        }

        using (var db = new PennyWiseDbContext(options))
        {
            var budgets = db.Budgets.OrderBy(b => b.StartDate).ToList();
            Assert.Equal(
                [(BudgetPeriod.Monthly, new DateOnly(2026, 9, 1), 4000m), (BudgetPeriod.Monthly, new DateOnly(2026, 10, 1), 4.43m)],
                budgets.Select(b => (b.Period, b.StartDate, b.Amount)));
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
