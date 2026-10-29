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

// What the browser learns about the signed-in user. Never the password hash,
// security stamp or lockout details.
public record UserResponse(int Id, string Name, string Email, DateTime MemberSince, string AllowancePeriod)
{
    public static UserResponse From(User u) => new(u.Id, u.Name, u.Email, u.CreatedAt, u.AllowancePeriod.ToString());
}
