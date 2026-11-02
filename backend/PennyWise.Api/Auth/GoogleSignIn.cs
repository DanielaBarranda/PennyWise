using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PennyWise.Api.Auth;

// Who Google says signed in.
public sealed record GoogleIdentity(string Subject, string Email, string Name);

// Google's public signing keys. Separate from the validator so tests can sign
// their own tokens with a key they made.
public interface IGoogleKeySource
{
    Task<IReadOnlyDictionary<string, RSAParameters>> GetKeysAsync(bool refresh, CancellationToken cancellationToken);
}

// Downloads Google's keys (a JSON Web Key Set) and keeps them for an hour.
// EC2 #2 has no internet access, so in production the URL points at Nginx on
// EC2 #1, which fetches https://www.googleapis.com/oauth2/v3/certs over HTTPS.
public sealed class HttpGoogleKeySource(IHttpClientFactory httpClients, IConfiguration configuration, TimeProvider time) : IGoogleKeySource
{
    private readonly string _url = configuration["PennyWise:GoogleCertsUrl"] ?? "https://www.googleapis.com/oauth2/v3/certs";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyDictionary<string, RSAParameters> _keys = new Dictionary<string, RSAParameters>();
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyDictionary<string, RSAParameters>> GetKeysAsync(bool refresh, CancellationToken cancellationToken)
    {
        var age = time.GetUtcNow() - _fetchedAt;
        // An unknown key ID means Google has rotated its keys: fetch again, but at
        // most once a minute so bad tokens cannot make the server hammer Google.
        if (age < TimeSpan.FromHours(1) && !(refresh && age > TimeSpan.FromMinutes(1))) return _keys;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var http = httpClients.CreateClient(nameof(HttpGoogleKeySource));
            using var response = await http.GetAsync(_url, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            var keys = new Dictionary<string, RSAParameters>();
            foreach (var key in json.RootElement.GetProperty("keys").EnumerateArray())
            {
                if (key.GetProperty("kty").GetString() != "RSA") continue;
                keys[key.GetProperty("kid").GetString()!] = new RSAParameters
                {
                    Modulus = Base64Url(key.GetProperty("n").GetString()!),
                    Exponent = Base64Url(key.GetProperty("e").GetString()!),
                };
            }
            _keys = keys;
            _fetchedAt = time.GetUtcNow();
            return _keys;
        }
        finally
        {
            _lock.Release();
        }
    }

    internal static byte[] Base64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}

// Checks the ID token that Google's sign-in button hands the browser: it must be
// signed by Google (RS256), issued for PennyWise's client ID, unexpired, and for
// an email address Google has verified. Anything else is rejected.
public sealed class GoogleTokenValidator(IGoogleKeySource keys, IConfiguration configuration, TimeProvider time, ILogger<GoogleTokenValidator> logger)
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    // Not a secret: it is also in the web page. Null turns Google sign-in off.
    public string? ClientId { get; } = configuration["PennyWise:GoogleClientId"] is { Length: > 0 } id ? id : null;

    public async Task<GoogleIdentity?> ValidateAsync(string credential, CancellationToken cancellationToken)
    {
        if (ClientId is null) return null;
        var parts = credential.Split('.');
        if (parts.Length != 3) return null;

        try
        {
            using var header = JsonDocument.Parse(HttpGoogleKeySource.Base64Url(parts[0]));
            if (header.RootElement.GetProperty("alg").GetString() != "RS256") return null;
            var kid = header.RootElement.GetProperty("kid").GetString() ?? "";

            var known = await keys.GetKeysAsync(false, cancellationToken);
            if (!known.ContainsKey(kid)) known = await keys.GetKeysAsync(true, cancellationToken);
            if (!known.TryGetValue(kid, out var key)) return null;

            using var rsa = RSA.Create(key);
            var signed = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            if (!rsa.VerifyData(signed, HttpGoogleKeySource.Base64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return null;

            using var payload = JsonDocument.Parse(HttpGoogleKeySource.Base64Url(parts[1]));
            var claims = payload.RootElement;
            var now = time.GetUtcNow();
            var issuer = claims.GetProperty("iss").GetString();
            if (issuer is not ("accounts.google.com" or "https://accounts.google.com")) return null;
            if (claims.GetProperty("aud").GetString() != ClientId) return null;
            if (DateTimeOffset.FromUnixTimeSeconds(claims.GetProperty("exp").GetInt64()) < now - ClockSkew) return null;
            if (claims.TryGetProperty("iat", out var iat) && DateTimeOffset.FromUnixTimeSeconds(iat.GetInt64()) > now + ClockSkew) return null;

            var verified = claims.TryGetProperty("email_verified", out var v)
                && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && v.GetString() == "true"));
            var subject = claims.GetProperty("sub").GetString();
            var email = claims.TryGetProperty("email", out var e) ? e.GetString() : null;
            if (!verified || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email)) return null;

            var name = claims.TryGetProperty("name", out var n) ? n.GetString() : null;
            return new GoogleIdentity(subject, email, string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name.Trim());
        }
        catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException or CryptographicException)
        {
            return null; // not a well-formed Google token
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not download Google's sign-in keys.");
            throw new GoogleUnavailableException();
        }
    }
}

public sealed class GoogleUnavailableException() : Exception("Google sign-in keys could not be downloaded.");
