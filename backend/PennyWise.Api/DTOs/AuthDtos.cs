using System.ComponentModel.DataAnnotations;
using PennyWise.Api.Auth;
using PennyWise.Api.Models;

namespace PennyWise.Api.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, ErrorMessage = "Name must be 80 characters or fewer.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254, ErrorMessage = "Email must be 254 characters or fewer.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [PasswordStrength]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(254)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(AuthRules.MaxPasswordLength)]
    public string? Password { get; set; }
}

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, ErrorMessage = "Name must be 80 characters or fewer.")]
    public string? Name { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "Enter your current password.")]
    [StringLength(AuthRules.MaxPasswordLength)]
    public string? CurrentPassword { get; set; }

    [Required(ErrorMessage = "Enter a new password.")]
    [PasswordStrength]
    public string? NewPassword { get; set; }

    [Required(ErrorMessage = "Confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}

// Deleting the account needs the password again, so an unattended signed-in
// browser cannot be used to erase someone's data. Accounts without a password
// (Google sign-in) confirm with Google instead.
public class DeleteAccountRequest
{
    [StringLength(AuthRules.MaxPasswordLength)]
    public string? Password { get; set; }

    [StringLength(GoogleSignInRequest.MaxCredentialLength)]
    public string? GoogleCredential { get; set; }
}

// The second sign-in step: the ticket from the first step plus a 6-digit code
// from the authenticator app or a recovery code.
public class TwoFactorLoginRequest
{
    [Required(ErrorMessage = "Your sign-in has expired. Log in again.")]
    [StringLength(2000)]
    public string? Ticket { get; set; }

    [Required(ErrorMessage = "Enter the code from your authenticator app.")]
    [StringLength(20, ErrorMessage = "Enter the 6-digit code or a recovery code.")]
    public string? Code { get; set; }
}

public class TwoFactorCodeRequest
{
    [Required(ErrorMessage = "Enter the code from your authenticator app.")]
    [StringLength(20, ErrorMessage = "Enter the 6-digit code or a recovery code.")]
    public string? Code { get; set; }
}

// The ID token Google's sign-in button gives the browser.
public class GoogleSignInRequest
{
    public const int MaxCredentialLength = 4096;

    [Required(ErrorMessage = "Google sign-in did not finish. Please try again.")]
    [StringLength(MaxCredentialLength)]
    public string? Credential { get; set; }
}

// Connecting Google to an account that already has a password: the password is
// asked for once, so nobody can take over an account just by making a Google
// account with the same email address.
public class GoogleLinkRequest
{
    [Required(ErrorMessage = "Your sign-in has expired. Try Continue with Google again.")]
    [StringLength(2000)]
    public string? Ticket { get; set; }

    [Required(ErrorMessage = "Enter your PennyWise password.")]
    [StringLength(AuthRules.MaxPasswordLength)]
    public string? Password { get; set; }
}

// Sent instead of the user when sign-in needs another step. Next is
// "twoFactor" (ask for the code) or "linkGoogle" (ask for the password once).
public record SignInStep(string Next, string Ticket, string? Email = null);

public record TwoFactorSetupResponse(string Key, string Uri);

public record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

// What the sign-in page needs to know before anyone signs in.
public record AuthOptionsResponse(string? GoogleClientId);

// What the browser learns about the signed-in user. Never the password hash,
// security stamp or lockout details.
// AvatarVersion is null when there is no profile photo; otherwise it changes
// whenever the photo does, and goes on the photo URL to bust the cache.
public record UserResponse(int Id, string Name, string Email, DateTime MemberSince, string BudgetPeriod, string? AvatarVersion,
    bool HasPassword, bool GoogleConnected, bool TwoFactorEnabled, int RecoveryCodesLeft)
{
    public static UserResponse From(User u) => new(u.Id, u.Name, u.Email, u.CreatedAt, u.BudgetPeriod.ToString(),
        u.AvatarUpdatedAt is { } t ? t.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
        u.HasPassword, u.GoogleSubject is not null, u.TwoFactorEnabled, TwoFactorService.RecoveryCodesLeft(u));
}
