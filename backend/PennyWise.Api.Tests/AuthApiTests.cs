using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class AuthApiTests : ApiTestBase
{
    [Fact]
    public async Task Register_signs_in_and_never_returns_password_data()
    {
        var client = NewClient();
        var response = await RegisterAsync(client, "new.user@pennywise.test", name: "Ana Cruz");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"name\":\"Ana Cruz\"", body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stamp", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Session_cookie_is_HttpOnly_and_SameSite_Strict()
    {
        var response = await RegisterAsync(NewClient(), "cookie@pennywise.test");
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("pennywise_session="));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("owner@pennywise.test")]
    [InlineData("  OWNER@PennyWise.test ")]
    public async Task Duplicate_email_is_rejected_ignoring_case_and_spaces(string email)
    {
        var response = await RegisterAsync(NewClient(), email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Email", problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("short1", "short1", "Password")]          // too short
    [InlineData("onlyletters", "onlyletters", "Password")] // no number
    [InlineData("12345678", "12345678", "Password")]       // no letter
    [InlineData("Budget2026", "Budget2027", "ConfirmPassword")]
    public async Task Weak_or_mismatched_passwords_are_rejected(string password, string confirm, string field)
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/register",
            new { name = "X", email = "weak@pennywise.test", password, confirmPassword = confirm });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("", "a@b.co", "Name")]
    [InlineData("Ana", "not-an-email", "Email")]
    public async Task Missing_name_or_invalid_email_is_rejected(string name, string email, string field)
    {
        var response = await NewClient().PostAsJsonAsync("/api/auth/register",
            new { name, email, password = DefaultPassword, confirmPassword = DefaultPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Login_and_logout_work()
    {
        var client = NewClient();

        var login = await LoginAsync(client, DefaultEmail);
        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");
        var logout = await client.PostAsync("/api/auth/logout", null);
        var meAfter = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(DefaultEmail, me!.Email);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, meAfter.StatusCode); // signed out: "nobody", not an error
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_answer()
    {
        var wrongPassword = await LoginAsync(NewClient(), DefaultEmail, "Wrong2026");
        var unknownEmail = await LoginAsync(NewClient(), "nobody@pennywise.test", "Wrong2026");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync().ContinueWith(t => Title(t.Result)),
            await unknownEmail.Content.ReadAsStringAsync().ContinueWith(t => Title(t.Result)));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_for_a_while()
    {
        var client = NewClient();
        for (var i = 0; i < 5; i++) await LoginAsync(client, DefaultEmail, "Wrong2026");

        var correctButLocked = await LoginAsync(client, DefaultEmail);

        Assert.Equal(HttpStatusCode.TooManyRequests, correctButLocked.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/expenses")]
    [InlineData("GET", "/api/budgets")]
    [InlineData("GET", "/api/dashboard/summary?month=2026-10")]
    [InlineData("GET", "/api/expenses/1")]
    [InlineData("DELETE", "/api/expenses/1")]
    public async Task Financial_endpoints_require_sign_in(string method, string url)
    {
        var response = await NewClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Who_am_I_answers_no_content_when_signed_out()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await NewClient().GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Health_check_stays_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await NewClient().GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task Changes_without_the_app_header_are_blocked()
    {
        // A signed-in browser, but a request like one a form on another website could send.
        using var bare = Factory.CreateClient(new() { HandleCookies = true });
        var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = DefaultEmail, password = DefaultPassword }),
        };
        login.Headers.Add("X-PennyWise-Client", "test");
        (await bare.SendAsync(login)).EnsureSuccessStatusCode();

        var response = await bare.PostAsJsonAsync("/api/expenses",
            new { amount = 1, category = "Food", description = "x", expenseDate = "2026-10-01" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses"))!);
    }

    private static string? Title(string json) =>
        System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("title").GetString();
}

// The per-IP limit on sign-in attempts, with a small limit so the test is quick.
public class AuthRateLimitTests() : ApiTestBase(new Dictionary<string, string> { ["PennyWise:AuthAttemptsPerMinute"] = "3" })
{
    public override Task InitializeAsync()
    {
        Client = NewClient();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Too_many_attempts_in_a_minute_are_refused()
    {
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++) codes.Add((await LoginAsync(Client, "someone@pennywise.test", "Wrong2026")).StatusCode);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], codes);
    }
}
