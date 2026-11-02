using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PennyWise.Api.Auth;
using PennyWise.Api.DTOs;

namespace PennyWise.Api.Tests;

// Stands in for Google: tokens are signed with a key made for the test, and the
// API is given that key instead of downloading Google's.
public sealed class FakeGoogle : IGoogleKeySource
{
    public const string ClientId = "test-client.apps.googleusercontent.com";
    public const string KeyId = "test-key";
    public readonly RSA Key = RSA.Create(2048);

    public Task<IReadOnlyDictionary<string, RSAParameters>> GetKeysAsync(bool refresh, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, RSAParameters>>(
            new Dictionary<string, RSAParameters> { [KeyId] = Key.ExportParameters(false) });

    public string Token(string subject, string email, string name = "Ana Cruz", bool emailVerified = true,
        string audience = ClientId, string issuer = "https://accounts.google.com", DateTimeOffset? expires = null, RSA? signWith = null)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = KeyId, typ = "JWT" }));
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = issuer, ["aud"] = audience, ["sub"] = subject, ["email"] = email, ["email_verified"] = emailVerified,
            ["name"] = name, ["iat"] = now.ToUnixTimeSeconds(), ["exp"] = (expires ?? now.AddHours(1)).ToUnixTimeSeconds(),
        }));
        var signature = (signWith ?? Key).SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{payload}.{Encode(signature)}";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public class GoogleSignInApiTests : ApiTestBase
{
    private static readonly FakeGoogle Google = new();

    public GoogleSignInApiTests()
        : base(new Dictionary<string, string> { ["PennyWise:GoogleClientId"] = FakeGoogle.ClientId },
            services: s => s.AddSingleton<IGoogleKeySource>(Google))
    {
    }

    private static Task<HttpResponseMessage> GoogleAsync(HttpClient client, string credential) =>
        client.PostAsJsonAsync("/api/auth/google", new { credential });

    [Fact]
    public async Task The_sign_in_page_gets_the_client_id()
    {
        var options = await NewClient().GetFromJsonAsync<AuthOptionsResponse>("/api/auth/options");
        Assert.Equal(FakeGoogle.ClientId, options!.GoogleClientId);
    }

    [Fact]
    public async Task A_new_Google_user_gets_an_account_without_a_password()
    {
        var browser = NewClient();

        var response = await GoogleAsync(browser, Google.Token("google-1", "ana@gmail.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = (await response.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.Equal(("Ana Cruz", "ana@gmail.com", false, true), (me.Name, me.Email, me.HasPassword, me.GoogleConnected));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/expenses")).StatusCode);

        // Signing in again finds the same account, even if the Gmail address changed.
        var again = await GoogleAsync(NewClient(), Google.Token("google-1", "ana.cruz@gmail.com"));
        Assert.Equal(me.Id, (await again.Content.ReadFromJsonAsync<UserResponse>())!.Id);
    }

    [Fact]
    public async Task A_Google_account_cannot_log_in_with_a_password()
    {
        await GoogleAsync(NewClient(), Google.Token("google-1", "ana@gmail.com"));

        var response = await LoginAsync(NewClient(), "ana@gmail.com", "");
        var withGuess = await LoginAsync(NewClient(), "ana@gmail.com", "Anything1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // password is required
        Assert.Equal(HttpStatusCode.Unauthorized, withGuess.StatusCode);
        Assert.Contains("Continue with Google", (await withGuess.Content.ReadFromJsonAsync<ProblemDetails>())!.Title);
    }

    [Theory]
    [InlineData("wrong-audience")]
    [InlineData("wrong-issuer")]
    [InlineData("expired")]
    [InlineData("unverified-email")]
    [InlineData("wrong-key")]
    [InlineData("garbage")]
    public async Task Tokens_not_from_Google_for_PennyWise_are_refused(string problem)
    {
        using var otherKey = RSA.Create(2048);
        var token = problem switch
        {
            "wrong-audience" => Google.Token("g", "ana@gmail.com", audience: "someone-else.apps.googleusercontent.com"),
            "wrong-issuer" => Google.Token("g", "ana@gmail.com", issuer: "https://evil.example"),
            "expired" => Google.Token("g", "ana@gmail.com", expires: DateTimeOffset.UtcNow.AddMinutes(-10)),
            "unverified-email" => Google.Token("g", "ana@gmail.com", emailVerified: false),
            "wrong-key" => Google.Token("g", "ana@gmail.com", signWith: otherKey),
            _ => "not.a.token",
        };

        var response = await GoogleAsync(NewClient(), token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_existing_password_account_needs_its_password_once_to_connect_Google()
    {
        var browser = NewClient();
        var response = await GoogleAsync(browser, Google.Token("google-owner", DefaultEmail.ToUpperInvariant()));

        var step = (await response.Content.ReadFromJsonAsync<SignInStep>())!;
        Assert.Equal(("linkGoogle", DefaultEmail), (step.Next, step.Email));
        Assert.Equal(HttpStatusCode.NoContent, (await browser.GetAsync("/api/auth/me")).StatusCode);

        var wrong = await browser.PostAsJsonAsync("/api/auth/google/link", new { ticket = step.Ticket, password = "Wrong2026" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var linked = await browser.PostAsJsonAsync("/api/auth/google/link", new { ticket = step.Ticket, password = DefaultPassword });
        var me = (await linked.Content.ReadFromJsonAsync<UserResponse>())!;
        Assert.True(me.GoogleConnected);
        Assert.True(me.HasPassword);

        // From now on Google signs straight in, and the password still works.
        var direct = await GoogleAsync(NewClient(), Google.Token("google-owner", DefaultEmail));
        Assert.Equal(me.Id, (await direct.Content.ReadFromJsonAsync<UserResponse>())!.Id);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(NewClient(), DefaultEmail)).StatusCode);
    }

    [Fact]
    public async Task Google_sign_in_still_asks_for_the_code_when_two_step_verification_is_on()
    {
        var link = (await (await GoogleAsync(Client, Google.Token("google-owner", DefaultEmail))).Content.ReadFromJsonAsync<SignInStep>())!;
        (await Client.PostAsJsonAsync("/api/auth/google/link", new { ticket = link.Ticket, password = DefaultPassword })).EnsureSuccessStatusCode();
        var setup = (await (await Client.PostAsync("/api/auth/two-factor/setup", null)).Content.ReadFromJsonAsync<TwoFactorSetupResponse>())!;
        var code = Totp.Code(Base32Decode(setup.Key), Totp.StepAt(DateTimeOffset.UtcNow));
        (await Client.PostAsJsonAsync("/api/auth/two-factor/enable", new { code })).EnsureSuccessStatusCode();

        var browser = NewClient();
        var response = await GoogleAsync(browser, Google.Token("google-owner", DefaultEmail));

        Assert.Equal("twoFactor", (await response.Content.ReadFromJsonAsync<SignInStep>())!.Next);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_Google_account_deletes_itself_by_confirming_with_Google()
    {
        var browser = NewClient();
        await GoogleAsync(browser, Google.Token("google-1", "ana@gmail.com"));

        var noProof = await browser.PostAsJsonAsync("/api/auth/delete-account", new { });
        var someoneElse = await browser.PostAsJsonAsync("/api/auth/delete-account", new { googleCredential = Google.Token("google-2", "ben@gmail.com") });
        Assert.Equal(HttpStatusCode.BadRequest, noProof.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, someoneElse.StatusCode);

        var deleted = await browser.PostAsJsonAsync("/api/auth/delete-account", new { googleCredential = Google.Token("google-1", "ana@gmail.com") });

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var fresh = await GoogleAsync(NewClient(), Google.Token("google-1", "ana@gmail.com"));
        Assert.NotNull((await fresh.Content.ReadFromJsonAsync<UserResponse>())!.Email); // a brand-new, empty account
    }

    [Fact]
    public async Task Google_users_have_no_password_to_change()
    {
        var browser = NewClient();
        await GoogleAsync(browser, Google.Token("google-1", "ana@gmail.com"));

        var response = await browser.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "x", newPassword = "Budget2027", confirmPassword = "Budget2027" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static byte[] Base32Decode(string key)
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
}

public class GoogleSignInOffTests : ApiTestBase
{
    [Fact]
    public async Task Without_a_client_id_Google_sign_in_is_off()
    {
        var options = await NewClient().GetFromJsonAsync<AuthOptionsResponse>("/api/auth/options");
        var response = await NewClient().PostAsJsonAsync("/api/auth/google", new { credential = "a.b.c" });

        Assert.Null(options!.GoogleClientId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
