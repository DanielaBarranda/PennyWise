using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using PennyWise.Api.Auth;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Starts the real API in memory against its own temporary SQLite file, so tests
// exercise the actual controllers, validation, sign-in, services and database,
// but never touch your development data.
public sealed class PennyWiseApiFactory(string databasePath, IDictionary<string, string>? settings = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PennyWise", $"Data Source={databasePath}");
        // Tests sign in many times a minute; the limit itself is tested separately.
        builder.UseSetting("PennyWise:AuthAttemptsPerMinute", "1000");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }
    }
}

// Every test starts with a fresh database and a signed-in default user (Client).
public abstract class ApiTestBase : IAsyncLifetime, IDisposable
{
    protected const string DefaultEmail = "owner@pennywise.test";
    protected const string DefaultPassword = "Budget2026";

    protected readonly string DatabasePath =
        Path.Combine(Path.GetTempPath(), $"pennywise-test-{Guid.NewGuid():N}.db");

    private readonly List<HttpClient> _clients = [];

    protected PennyWiseApiFactory Factory { get; set; }
    protected HttpClient Client { get; set; } = null!;

    protected ApiTestBase(IDictionary<string, string>? settings = null)
    {
        Factory = new PennyWiseApiFactory(DatabasePath, settings);
    }

    public virtual async Task InitializeAsync()
    {
        Client = NewClient();
        await RegisterAsync(Client, DefaultEmail, DefaultPassword);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // A browser-like client: keeps cookies and sends the header the web app sends.
    protected HttpClient NewClient()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add(ClientHeaderMiddleware.HeaderName, "test");
        _clients.Add(client);
        return client;
    }

    protected static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password = DefaultPassword, string name = "Test User")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { name, email, password, confirmPassword = password });
        return response;
    }

    protected static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = DefaultPassword) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    // A second, separate account in its own "browser".
    protected async Task<HttpClient> NewUserAsync(string email)
    {
        var client = NewClient();
        (await RegisterAsync(client, email)).EnsureSuccessStatusCode();
        return client;
    }

    // Simulates stopping and starting the API process with the same database file,
    // then signing in again.
    protected async Task RestartApiAsync()
    {
        DisposeClientsAndFactory();
        Factory = new PennyWiseApiFactory(DatabasePath);
        Client = NewClient();
        (await LoginAsync(Client, DefaultEmail)).EnsureSuccessStatusCode();
    }

    protected Task<ExpenseResponse> CreateExpenseAsync(decimal amount, string category, string date, string description = "Test expense") =>
        CreateExpenseAsync(Client, amount, category, date, description);

    protected static async Task<ExpenseResponse> CreateExpenseAsync(HttpClient client, decimal amount, string category, string date, string description = "Test expense")
    {
        var response = await client.PostAsJsonAsync("/api/expenses", new { amount, category, description, expenseDate = date });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>())!;
    }

    protected Task<BudgetResponse> CreateBudgetAsync(string month, decimal amount, params (string Category, decimal Amount)[] limits) =>
        CreateBudgetAsync(Client, month, amount, limits);

    protected static async Task<BudgetResponse> CreateBudgetAsync(HttpClient client, string month, decimal amount, params (string Category, decimal Amount)[] limits)
    {
        var response = await client.PostAsJsonAsync("/api/budgets", new
        {
            month,
            amount,
            categoryLimits = limits.Select(l => new { category = l.Category, amount = l.Amount }),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BudgetResponse>())!;
    }

    protected Task<DashboardSummaryResponse> GetSummaryAsync(string month) => GetSummaryAsync(Client, month);

    protected static async Task<DashboardSummaryResponse> GetSummaryAsync(HttpClient client, string month) =>
        (await client.GetFromJsonAsync<DashboardSummaryResponse>($"/api/dashboard/summary?month={month}"))!;

    private void DisposeClientsAndFactory()
    {
        foreach (var client in _clients) client.Dispose();
        _clients.Clear();
        Factory.Dispose();
        SqliteConnection.ClearAllPools(); // Release the file lock so Windows can reuse or delete it.
    }

    public void Dispose()
    {
        DisposeClientsAndFactory();
        foreach (var file in new[] { DatabasePath, DatabasePath + "-shm", DatabasePath + "-wal" })
        {
            try { File.Delete(file); } catch (IOException) { /* temp folder; safe to leave */ }
        }
        GC.SuppressFinalize(this);
    }
}
