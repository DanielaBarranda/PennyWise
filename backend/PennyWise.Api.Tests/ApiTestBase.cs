using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Starts the real API in memory against its own temporary SQLite file, so tests
// exercise the actual controllers, validation, services and database, but
// never touch your development data.
public sealed class PennyWiseApiFactory(string databasePath) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PennyWise", $"Data Source={databasePath}");
    }
}

public abstract class ApiTestBase : IDisposable
{
    protected readonly string DatabasePath =
        Path.Combine(Path.GetTempPath(), $"pennywise-test-{Guid.NewGuid():N}.db");

    protected PennyWiseApiFactory Factory { get; private set; }
    protected HttpClient Client { get; private set; }

    protected ApiTestBase()
    {
        Factory = new PennyWiseApiFactory(DatabasePath);
        Client = Factory.CreateClient();
    }

    // Simulates stopping and starting the API process with the same database file.
    protected void RestartApi()
    {
        Client.Dispose();
        Factory.Dispose();
        SqliteConnection.ClearAllPools();
        Factory = new PennyWiseApiFactory(DatabasePath);
        Client = Factory.CreateClient();
    }

    protected async Task<ExpenseResponse> CreateExpenseAsync(decimal amount, string category, string date, string description = "Test expense")
    {
        var response = await Client.PostAsJsonAsync("/api/expenses", new { amount, category, description, expenseDate = date });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>())!;
    }

    protected async Task<BudgetResponse> CreateBudgetAsync(string month, decimal amount, params (string Category, decimal Amount)[] limits)
    {
        var response = await Client.PostAsJsonAsync("/api/budgets", new
        {
            month,
            amount,
            categoryLimits = limits.Select(l => new { category = l.Category, amount = l.Amount }),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BudgetResponse>())!;
    }

    protected async Task<DashboardSummaryResponse> GetSummaryAsync(string month) =>
        (await Client.GetFromJsonAsync<DashboardSummaryResponse>($"/api/dashboard/summary?month={month}"))!;

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        SqliteConnection.ClearAllPools(); // Release the file lock so Windows can delete it.
        foreach (var file in new[] { DatabasePath, DatabasePath + "-shm", DatabasePath + "-wal" })
        {
            try { File.Delete(file); } catch (IOException) { /* temp folder; safe to leave */ }
        }
        GC.SuppressFinalize(this);
    }
}
