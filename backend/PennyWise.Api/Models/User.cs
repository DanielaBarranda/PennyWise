namespace PennyWise.Api.Models;

// A PennyWise account. The password is never stored: only a salted hash made by
// ASP.NET Core's PasswordHasher (PBKDF2), which is never sent to the browser.
public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // As typed (for display) and normalized (trimmed, upper-case) for unique lookups.
    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    // Changes whenever the password changes; sign-in cookies carry a copy, so an
    // old cookie stops working once the stamp no longer matches.
    public string SecurityStamp { get; set; } = string.Empty;

    // Lockout after repeated wrong passwords (brute-force protection).
    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEndsAt { get; set; }

    // How the dashboard shows the suggested spending allowance. A display
    // preference only: it never changes the monthly budget.
    public AllowancePeriod AllowancePeriod { get; set; } = AllowancePeriod.Monthly;

    // When the profile photo last changed (null = no photo). The browser adds it
    // to the photo URL, so a new photo is never hidden by an old cached one.
    public DateTime? AvatarUpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum AllowancePeriod
{
    Daily,
    Weekly,
    Monthly,
}
