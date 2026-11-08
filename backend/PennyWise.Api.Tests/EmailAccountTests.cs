using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using PennyWise.Api.DTOs;
using PennyWise.Api.Services.Email;

namespace PennyWise.Api.Tests;

// Keeps every email instead of sending it, so tests can open the links.
public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public bool IsEnabled => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    // Emails go out in the background; wait briefly for the one we expect.
    public async Task<EmailMessage> WaitForAsync(string to, string subjectContains)
    {
        for (var i = 0; i < 100; i++)
        {
            var found = Sent.FirstOrDefault(m => m.To == to && m.Subject.Contains(subjectContains));
            if (found is not null) return found;
            await Task.Delay(20);
        }
        throw new Xunit.Sdk.XunitException($"No email \"{subjectContains}\" was sent to {to}.");
    }

    public static string Token(EmailMessage message) =>
        Uri.UnescapeDataString(Regex.Match(message.Text, @"token=([^\s]+)").Groups[1].Value);
}

public class EmailAccountTests() : ApiTestBase(services: s => s.AddSingleton<IEmailSender, CapturingEmailSender>())
{
    private CapturingEmailSender Mail => (CapturingEmailSender)Factory.Services.GetRequiredService<IEmailSender>();

    private async Task<UserResponse> MeAsync(HttpClient client) => (await client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;

    [Fact]
    public async Task Signing_up_sends_a_confirmation_link_that_confirms_the_email()
    {
        Assert.False((await MeAsync(Client)).EmailVerified);
        var email = await Mail.WaitForAsync(DefaultEmail, "Confirm your email");
        Assert.Contains("/#/verify-email?token=", email.Text);

        var anonymous = NewClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/verify-email", new { token = CapturingEmailSender.Token(email) });

        response.EnsureSuccessStatusCode();
        Assert.True((await MeAsync(Client)).EmailVerified);
    }

    [Fact]
    public async Task A_broken_confirmation_link_is_refused()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/verify-email", new { token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await MeAsync(Client)).EmailVerified);
    }

    [Fact]
    public async Task The_confirmation_link_can_be_sent_again_until_the_email_is_confirmed()
    {
        await Mail.WaitForAsync(DefaultEmail, "Confirm your email");

        Assert.Equal(HttpStatusCode.Accepted, (await Client.PostAsync("/api/auth/resend-verification", null)).StatusCode);
        for (var i = 0; i < 100 && Mail.Sent.Count(m => m.Subject.Contains("Confirm")) < 2; i++) await Task.Delay(20);
        var latest = Mail.Sent.Last(m => m.Subject.Contains("Confirm"));
        await Client.PostAsJsonAsync("/api/auth/verify-email", new { token = CapturingEmailSender.Token(latest) });

        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsync("/api/auth/resend-verification", null)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_sends_a_one_time_link_that_sets_a_new_password()
    {
        var anonymous = NewClient();
        var asked = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new { email = DefaultEmail.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
        var email = await Mail.WaitForAsync(DefaultEmail, "Reset your PennyWise password");
        var token = CapturingEmailSender.Token(email);

        var reset = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "NewBudget2027", confirmPassword = "NewBudget2027" });
        reset.EnsureSuccessStatusCode();

        // The old session is signed out, the old password stops working, the new one works.
        Assert.Equal(HttpStatusCode.NoContent, (await Client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(NewClient(), DefaultEmail, DefaultPassword)).StatusCode);
        var fresh = NewClient();
        (await LoginAsync(fresh, DefaultEmail, "NewBudget2027")).EnsureSuccessStatusCode();
        Assert.True((await MeAsync(fresh)).EmailVerified);
        await Mail.WaitForAsync(DefaultEmail, "password was changed");

        // The same link cannot be used twice.
        var again = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "Another2028x", confirmPassword = "Another2028x" });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_answers_the_same_for_an_unknown_email_and_sends_nothing()
    {
        await Mail.WaitForAsync(DefaultEmail, "Confirm your email");
        var before = Mail.Sent.Count;

        var response = await NewClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@pennywise.test" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await Task.Delay(200);
        Assert.Equal(before, Mail.Sent.Count);
    }

    [Fact]
    public async Task A_reset_link_stops_working_once_the_password_is_changed_another_way()
    {
        await NewClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = DefaultEmail });
        var token = CapturingEmailSender.Token(await Mail.WaitForAsync(DefaultEmail, "Reset your"));
        (await Client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = DefaultPassword, newPassword = "Changed2027", confirmPassword = "Changed2027" })).EnsureSuccessStatusCode();

        var reset = await NewClient().PostAsJsonAsync("/api/auth/reset-password", new { token, password = "NewBudget2027", confirmPassword = "NewBudget2027" });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task A_new_password_from_a_reset_link_must_still_be_strong()
    {
        await NewClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = DefaultEmail });
        var token = CapturingEmailSender.Token(await Mail.WaitForAsync(DefaultEmail, "Reset your"));

        var reset = await NewClient().PostAsJsonAsync("/api/auth/reset-password", new { token, password = "short", confirmPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }
}

// Without an SMTP host (production with no email set up), nothing pretends to work.
public class EmailDisabledTests : ApiTestBase
{
    [Fact]
    public async Task Without_email_the_app_says_so_instead_of_pretending()
    {
        var options = await Client.GetFromJsonAsync<AuthOptionsResponse>("/api/auth/options");
        Assert.False(options!.EmailEnabled);

        var forgot = await NewClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = DefaultEmail });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, forgot.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Client.PostAsync("/api/auth/resend-verification", null)).StatusCode);
    }
}
