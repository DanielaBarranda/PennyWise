using System.ComponentModel.DataAnnotations;

namespace PennyWise.Api.Auth;

public static class AuthRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Emails are compared trimmed and upper-cased, so "Ana@Mail.com " and
    // "ana@mail.com" are the same account.
    public static string Normalize(string email) => email.Trim().ToUpperInvariant();
}

// At least 8 characters with at least one letter and one number. The same rule
// is shown on the registration page.
public sealed class PasswordStrengthAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string password) return ValidationResult.Success; // [Required] reports missing values.

        if (password.Length < AuthRules.MinPasswordLength || password.Length > AuthRules.MaxPasswordLength
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return new ValidationResult(
                $"Password must be {AuthRules.MinPasswordLength}–{AuthRules.MaxPasswordLength} characters and include at least one letter and one number.",
                [context.MemberName!]);
        }
        return ValidationResult.Success;
    }
}
