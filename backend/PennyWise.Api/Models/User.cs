namespace PennyWise.Api.Models;

// A PennyWise account. The password is never stored: only a salted hash made by
// ASP.NET Core's PasswordHasher (PBKDF2), which is never sent to the browser.
// Accounts created with Google sign-in have no password (an empty hash).
public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // As typed (for display) and normalized (trimmed, upper-case) for unique lookups.
    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool HasPassword => PasswordHash.Length > 0;

    // Google's permanent ID for the person ("sub" in its sign-in token), once
    // Google sign-in is connected. Emails can change; this never does.
    public string? GoogleSubject { get; set; }

    // Two-step verification with an authenticator app. The secret is encrypted
    // with ASP.NET Data Protection; recovery codes are stored only as hashes.
    public string? TwoFactorSecret { get; set; }

    // A secret shown during setup that is not active until a code from it is entered.
    public string? TwoFactorPendingSecret { get; set; }

    public DateTime? TwoFactorEnabledAt { get; set; }

    public bool TwoFactorEnabled => TwoFactorEnabledAt is not null;

    // SHA-256 hashes of the unused recovery codes, separated by ";".
    public string? RecoveryCodeHashes { get; set; }

    // The last 30-second time step a code was accepted for, so a code cannot be used twice.
    public long? LastTotpStep { get; set; }

    // Changes whenever the password changes; sign-in cookies carry a copy, so an
    // old cookie stops working once the stamp no longer matches.
    public string SecurityStamp { get; set; } = string.Empty;

    // Lockout after repeated wrong passwords (brute-force protection).
    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEndsAt { get; set; }

    // Whether the dashboard works with daily, weekly or monthly budgets. Stored
    // in the column "AllowancePeriod" (its name before budgets had periods).
    public BudgetPeriod BudgetPeriod { get; set; } = BudgetPeriod.Monthly;

    // When the profile photo last changed (null = no photo). The browser adds it
    // to the photo URL, so a new photo is never hidden by an old cached one.
    public DateTime? AvatarUpdatedAt { get; set; }

    // When the app tour was finished or skipped (null = show it on the next visit).
    // Kept on the account, so it does not come back on another device.
    public DateTime? TourCompletedAt { get; set; }

    // When the user proved they own the email (by its confirmation link, a
    // password reset link, or Google, which only gives verified emails).
    public DateTime? EmailVerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
