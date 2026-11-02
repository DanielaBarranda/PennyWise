using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using PennyWise.Api.Models;
using PennyWise.Api.Services;

namespace PennyWise.Api.Auth;

// Two-step verification: the authenticator-app secret, the one-time recovery
// codes, and the short-lived "tickets" that carry a half-finished sign-in from
// the password (or Google) step to the code step.
public sealed class TwoFactorService(IDataProtectionProvider dataProtection, AppClock clock)
{
    public const int RecoveryCodeCount = 10;
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(5);

    // The secret is encrypted with the same Data Protection keys that sign the
    // session cookie, so a copy of the database alone cannot produce codes.
    private readonly IDataProtector _secrets = dataProtection.CreateProtector("PennyWise.TwoFactor.Secret");
    private readonly ITimeLimitedDataProtector _tickets =
        dataProtection.CreateProtector("PennyWise.TwoFactor.Ticket").ToTimeLimitedDataProtector();

    // A new secret, kept as pending until the user proves their app has it.
    public (string Key, string Uri) BeginSetup(User user)
    {
        var secret = Totp.NewSecret();
        user.TwoFactorPendingSecret = _secrets.Protect(Convert.ToBase64String(secret));
        return (Totp.Base32(secret), Totp.SetupUri(secret, user.Email));
    }

    // Turns two-step verification on when the code matches the pending secret.
    // Returns the recovery codes (shown once), or null if the code is wrong.
    public IReadOnlyList<string>? TryEnable(User user, string code)
    {
        if (user.TwoFactorPendingSecret is null) return null;
        var secret = Unprotect(user.TwoFactorPendingSecret);
        var step = Totp.Verify(secret, Clean(code), clock.UtcNow);
        if (step is null) return null;

        user.TwoFactorSecret = user.TwoFactorPendingSecret;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorEnabledAt = clock.UtcNow;
        user.LastTotpStep = step;
        return NewRecoveryCodes(user);
    }

    public void Disable(User user)
    {
        user.TwoFactorSecret = null;
        user.TwoFactorPendingSecret = null;
        user.TwoFactorEnabledAt = null;
        user.RecoveryCodeHashes = null;
        user.LastTotpStep = null;
    }

    // A 6-digit code from the app, or one of the recovery codes (which is then
    // used up). A code from the app works once: the same 30-second code is
    // refused the second time.
    public bool Verify(User user, string code)
    {
        if (user.TwoFactorSecret is null) return false;
        var cleaned = Clean(code);
        if (cleaned.Length == Totp.Digits)
        {
            var step = Totp.Verify(Unprotect(user.TwoFactorSecret), cleaned, clock.UtcNow);
            if (step is null || step <= user.LastTotpStep) return false;
            user.LastTotpStep = step;
            return true;
        }

        var hashes = RecoveryHashes(user);
        var hash = Hash(cleaned);
        if (!hashes.Remove(hash)) return false;
        user.RecoveryCodeHashes = string.Join(';', hashes);
        return true;
    }

    // Replaces every recovery code; the old ones stop working.
    public IReadOnlyList<string> NewRecoveryCodes(User user)
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789"; // no 0/o, 1/l/i
        var codes = Enumerable.Range(0, RecoveryCodeCount)
            .Select(_ => RandomNumberGenerator.GetString(alphabet, 10))
            .Select(c => $"{c[..5]}-{c[5..]}")
            .ToList();
        user.RecoveryCodeHashes = string.Join(';', codes.Select(c => Hash(Clean(c))));
        return codes;
    }

    public static int RecoveryCodesLeft(User user) => RecoveryHashes(user).Count;

    // A ticket names the user and their security stamp at the time, so it stops
    // working if the password changes in the meantime. `extra` carries anything
    // else the next step needs (the Google ID when connecting Google).
    public string CreateTicket(User user, string purpose, string extra = "") =>
        _tickets.Protect($"{purpose}|{user.Id}|{user.SecurityStamp}|{extra}", TicketLifetime);

    public (int UserId, string Stamp, string Extra)? ReadTicket(string? ticket, string purpose)
    {
        if (string.IsNullOrEmpty(ticket)) return null;
        try
        {
            var parts = _tickets.Unprotect(ticket).Split('|', 4);
            if (parts.Length != 4 || parts[0] != purpose || !int.TryParse(parts[1], out var userId)) return null;
            return (userId, parts[2], parts[3]);
        }
        catch (CryptographicException)
        {
            return null; // expired, tampered with, or not a ticket at all
        }
    }

    private byte[] Unprotect(string protectedSecret) => Convert.FromBase64String(_secrets.Unprotect(protectedSecret));

    // People type codes with spaces or dashes ("123 456", "abcde-fghij").
    private static string Clean(string code) =>
        new string(code.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();

    private static List<string> RecoveryHashes(User user) =>
        (user.RecoveryCodeHashes ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

    // Recovery codes are random (about 49 bits each), so a plain SHA-256 is enough.
    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(code)));
}
