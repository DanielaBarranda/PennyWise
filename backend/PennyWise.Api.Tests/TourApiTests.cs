using System.Net;
using System.Net.Http.Json;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class TourApiTests : ApiTestBase
{
    private async Task<UserResponse> MeAsync(HttpClient client) => (await client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;

    [Fact]
    public async Task A_new_account_has_not_seen_the_tour()
    {
        Assert.False((await MeAsync(Client)).TourCompleted);
    }

    [Fact]
    public async Task Finishing_or_skipping_is_remembered_on_every_device()
    {
        var response = await Client.PutAsJsonAsync("/api/auth/tour", new { completed = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<UserResponse>())!.TourCompleted);
        var phone = NewClient();
        (await LoginAsync(phone, DefaultEmail)).EnsureSuccessStatusCode();
        Assert.True((await MeAsync(phone)).TourCompleted);
    }

    [Fact]
    public async Task Take_the_tour_can_reset_it()
    {
        await Client.PutAsJsonAsync("/api/auth/tour", new { completed = true });

        var response = await Client.PutAsJsonAsync("/api/auth/tour", new { completed = false });

        Assert.False((await response.Content.ReadFromJsonAsync<UserResponse>())!.TourCompleted);
    }

    [Fact]
    public async Task It_only_changes_the_signed_in_user()
    {
        var other = await NewUserAsync("other@pennywise.test");

        await Client.PutAsJsonAsync("/api/auth/tour", new { completed = true });

        Assert.False((await MeAsync(other)).TourCompleted);
    }

    [Fact]
    public async Task It_needs_a_value_and_a_signed_in_user()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync("/api/auth/tour", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().PutAsJsonAsync("/api/auth/tour", new { completed = true })).StatusCode);
    }
}
