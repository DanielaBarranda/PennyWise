using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// A clock that always says the same time.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

// Base for the savings, bills, allowance, insights and report tests:
// "today" is Tuesday 20 October 2026, 10:00 in Manila (02:00 UTC).
// October has 31 days, so 12 days are left including today, and Sunday is
// 25 October (6 days left in the week including today).
public abstract class FeatureTestBase(DateTimeOffset? now = null)
    : ApiTestBase(time: new FixedTimeProvider(now ?? new DateTimeOffset(2026, 10, 20, 2, 0, 0, TimeSpan.Zero)))
{
    protected static readonly DateOnly Today = new(2026, 10, 20);

    protected Task<SavingsGoalResponse> CreateGoalAsync(string name, decimal target, string? targetDate = null) =>
        PostAsync<SavingsGoalResponse>("/api/savings", new { name, targetAmount = target, targetDate });

    protected Task<SavingsGoalResponse> AddSavingsAsync(int goalId, string type, decimal amount, string date = "2026-10-20") =>
        PostAsync<SavingsGoalResponse>($"/api/savings/{goalId}/transactions", new { type, amount, date });

    protected Task<BillResponse> CreateBillAsync(string name, decimal amount, string frequency, string firstDueDate,
        string category = "Bills", string? endDate = null) =>
        PostAsync<BillResponse>("/api/bills", new { name, amount, category, frequency, firstDueDate, endDate });

    protected async Task<List<BillOccurrenceResponse>> OccurrencesAsync(string from, string to) =>
        (await Client.GetFromJsonAsync<List<BillOccurrenceResponse>>($"/api/bills/occurrences?from={from}&to={to}"))!;

    protected async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await Client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
