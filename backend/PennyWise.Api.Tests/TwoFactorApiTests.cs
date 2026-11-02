using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

public class TwoFactorApiTests : ApiTestBase
{
    // Base32 back to bytes, the way an authenticator app reads the setup key.
    private static byte[] FromBase32(string key)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in key)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. bytes];
    }

    // What the phone shows now, or `offset` 30-second steps away.
    private static string CodeFor(byte[] secret, int offset = 0) => Totp.Code(secret, Totp.StepAt(DateTimeOffset.UtcNow) + offset);

    private async Task<(byte[] Secret, IReadOnlyList<string> RecoveryCodes)> TurnOnAsync()
    {
        var setup = await (await Client.PostAsync("/api/auth/two-factor/setup", null)).Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        var secret = FromBase32(setup!.Key);
        var response = await Client.PostAsJsonAsync("/api/auth/two-factor/enable", new { code = CodeFor(secret) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = (await response.Content.ReadFromJsonAsync<RecoveryCodesResponse>())!.RecoveryCodes;
        return (secret, codes);
    }

    private static async Task<string> PasswordStepAsync(HttpClient client)
    {
        var response = await LoginAsync(client, DefaultEmail);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var step = (await response.Content.ReadFromJsonAsync<SignInStep>())!;
        Assert.Equal("twoFactor", step.Next);
        return step.Ticket;
    }

    [Fact]
    public async Task Setup_gives_a_key_and_an_authenticator_link_but_changes_nothing_until_confirmed()
    {
        var setup = await (await Client.PostAsync("/api/auth/two-factor/setup", null)).Content.ReadFromJsonAsync<TwoFactorSetupResponse>();

        Assert.Equal(32, setup!.Key.Length); // 20 random bytes in Base32
        Assert.StartsWith("otpauth://totp/PennyWise%3Aowner%40pennywise.test?secret=" + setup.Key, setup.Uri);
        var login = await LoginAsync(NewClient(), DefaultEmail);
        Assert.NotNull((await login.Content.ReadFromJsonAsync<UserResponse>())!.Email); // still a one-step sign-in
    }

    [Fact]
    public async Task A_wrong_code_does_not_turn_it_on()
    {
        await Client.PostAsync("/api/auth/two-factor/setup", null);

        var response = await Client.PostAsJsonAsync("/api/auth/two-factor/enable", new { code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("not correct", problem!.Errors["Code"][0]);
        Assert.False((await Client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!.TwoFactorEnabled);
    }

    [Fact]
    public async Task Turning_it_on_gives_ten_recovery_codes_and_signs_out_other_devices()
    {
        var other = NewClient();
        (await LoginAsync(other, DefaultEmail)).EnsureSuccessStatusCode();

        var (_, codes) = await TurnOnAsync();

        Assert.Equal(10, codes.Count);
        Assert.All(codes, c => Assert.Matches("^[a-z2-9]{5}-[a-z2-9]{5}$", c));
        var me = (await Client.GetFromJsonAsync<UserResponse>("/api/auth/me"))!;
        Assert.True(me.TwoFactorEnabled);
        Assert.Equal(10, me.RecoveryCodesLeft);
        Assert.Equal(HttpStatusCode.NoContent, (await other.GetAsync("/api/auth/me")).StatusCode);

        // Nothing secret is stored in plain text.
        using var scope = Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<PennyWiseDbContext>().Users.SingleAsync();
        Assert.DoesNotContain(codes[0].Replace("-", ""), user.RecoveryCodeHashes!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(user.TwoFactorPendingSecret);
    }

    [Fact]
    public async Task With_it_on_the_password_alone_does_not_sign_in()
    {
        await TurnOnAsync();
        var browser = NewClient();

        await PasswordStepAsync(browser);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/expenses")).StatusCode);
    }

    [Fact]
    public async Task The_code_from_the_app_finishes_signing_in()
    {
        var (secret, _) = await TurnOnAsync();
        var browser = NewClient();
        var ticket = await PasswordStepAsync(browser);

        var response = await browser.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket, code = CodeFor(secret, 1) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DefaultEmail, (await response.Content.ReadFromJsonAsync<UserResponse>())!.Email);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/expenses")).StatusCode);
    }

    [Fact]
    public async Task The_same_code_cannot_be_used_twice()
    {
        var (secret, _) = await TurnOnAsync();
        var code = CodeFor(secret, 1);
        var first = NewClient();
        (await first.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket = await PasswordStepAsync(first), code })).EnsureSuccessStatusCode();

        var second = NewClient();
        var response = await second.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket = await PasswordStepAsync(second), code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_works_once()
    {
        var (_, codes) = await TurnOnAsync();
        var first = NewClient();
        var ok = await first.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket = await PasswordStepAsync(first), code = codes[3].ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(9, (await ok.Content.ReadFromJsonAsync<UserResponse>())!.RecoveryCodesLeft);

        var second = NewClient();
        var again = await second.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket = await PasswordStepAsync(second), code = codes[3] });

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task A_made_up_or_expired_ticket_is_refused()
    {
        var (secret, _) = await TurnOnAsync();

        var response = await NewClient().PostAsJsonAsync("/api/auth/login/two-factor", new { ticket = "not-a-ticket", code = CodeFor(secret, 1) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_cancels_a_half_finished_sign_in()
    {
        var (secret, _) = await TurnOnAsync();
        var attacker = NewClient();
        var ticket = await PasswordStepAsync(attacker);

        (await Client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = DefaultPassword, newPassword = "Budget2027", confirmPassword = "Budget2027" })).EnsureSuccessStatusCode();
        var response = await attacker.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket, code = CodeFor(secret, 1) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Guessing_codes_locks_the_account_even_with_the_right_password()
    {
        await TurnOnAsync();
        var attacker = NewClient();

        // The right password in between must not reset the count of wrong codes.
        for (var i = 0; i < AuthRules.MaxFailedLogins; i++)
        {
            var ticket = await PasswordStepAsync(attacker);
            await attacker.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket, code = "000000" });
        }

        var locked = await LoginAsync(attacker, DefaultEmail);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Contains("Too many wrong attempts", (await locked.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Fact]
    public async Task Turning_it_off_needs_a_code()
    {
        var (secret, _) = await TurnOnAsync();

        var wrong = await Client.PostAsJsonAsync("/api/auth/two-factor/disable", new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var right = await Client.PostAsJsonAsync("/api/auth/two-factor/disable", new { code = CodeFor(secret, 1) });
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.False((await right.Content.ReadFromJsonAsync<UserResponse>())!.TwoFactorEnabled);
        var login = await LoginAsync(NewClient(), DefaultEmail);
        Assert.Equal(DefaultEmail, (await login.Content.ReadFromJsonAsync<UserResponse>())!.Email);
    }

    [Fact]
    public async Task A_recovery_code_can_turn_it_off_after_a_rejected_code()
    {
        var (secret, codes) = await TurnOnAsync();
        await Client.PostAsJsonAsync("/api/auth/two-factor/disable", new { code = CodeFor(secret, -1) });

        var response = await Client.PostAsJsonAsync("/api/auth/two-factor/disable", new { code = codes[5] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<UserResponse>())!.TwoFactorEnabled);
    }

    [Fact]
    public async Task New_recovery_codes_replace_the_old_ones()
    {
        var (secret, oldCodes) = await TurnOnAsync();

        var response = await Client.PostAsJsonAsync("/api/auth/two-factor/recovery-codes", new { code = CodeFor(secret, 1) });
        var newCodes = (await response.Content.ReadFromJsonAsync<RecoveryCodesResponse>())!.RecoveryCodes;

        Assert.Equal(10, newCodes.Count);
        var browser = NewClient();
        var old = await browser.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket = await PasswordStepAsync(browser), code = oldCodes[0] });
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        var fresh = await browser.PostAsJsonAsync("/api/auth/login/two-factor", new { ticket = await PasswordStepAsync(browser), code = newCodes[0] });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
    }

    [Fact]
    public void Codes_match_the_RFC_6238_test_vectors()
    {
        var secret = "12345678901234567890"u8.ToArray();
        // RFC 6238 appendix B (SHA-1), last 6 digits.
        Assert.Equal("287082", Totp.Code(secret, 59 / 30));
        Assert.Equal("081804", Totp.Code(secret, 1111111109 / 30));
        Assert.Equal("005924", Totp.Code(secret, 1234567890 / 30));
        Assert.Equal("279037", Totp.Code(secret, 2000000000 / 30));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Totp.Base32(secret));
    }
}
