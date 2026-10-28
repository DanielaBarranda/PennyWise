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

// What the browser learns about the signed-in user. Never the password hash,
// security stamp or lockout details.
public record UserResponse(int Id, string Name, string Email)
{
    public static UserResponse From(User u) => new(u.Id, u.Name, u.Email);
}
