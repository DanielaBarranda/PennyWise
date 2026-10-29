using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Services;

namespace PennyWise.Api.Controllers;

// Sign-up, sign-in and sign-out. A successful sign-in sets an HttpOnly cookie that
// the browser sends automatically; JavaScript can never read it.
[ApiController]
[Route("api/auth")]
public class AuthController(
    PennyWiseDbContext db,
    IPasswordHasher<User> hasher,
    LegacyDataClaimer legacyData,
    AppClock clock) : ControllerBase
{
    public const string StampClaim = "pennywise:stamp";
    private const string WrongCredentials = "Email or password is incorrect.";

    // Hash of a random password, checked when an email is unknown so a wrong
    // email takes as long as a wrong password (no timing hint that it exists).
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString());

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request)
    {
        var email = request.Email!.Trim();
        var normalized = AuthRules.Normalize(email);
        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized))
        {
            return Conflict(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["Email"] = ["An account with this email already exists. Log in instead."],
            })
            { Status = StatusCodes.Status409Conflict, Title = "This email is already registered." });
        }

        var now = clock.UtcNow;
        var user = new User
        {
            Name = request.Name!.Trim(),
            Email = email,
            NormalizedEmail = normalized,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            // Two sign-ups with the same email at the same moment.
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "This email is already registered." });
        }

        await legacyData.ClaimIfOwnerAsync(user);
        await SignInAsync(user);
        return CreatedAtAction(nameof(Me), UserResponse.From(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<UserResponse>> Login(LoginRequest request)
    {
        var normalized = AuthRules.Normalize(request.Email!);
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
        if (user is null)
        {
            hasher.VerifyHashedPassword(new User(), DummyHash, request.Password!);
            return Unauthorized(Failure(WrongCredentials));
        }

        var now = clock.UtcNow;
        if (user.LockoutEndsAt > now)
        {
            var minutes = (int)Math.Ceiling((user.LockoutEndsAt.Value - now).TotalMinutes);
            return StatusCode(StatusCodes.Status429TooManyRequests,
                Failure($"Too many wrong passwords. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}."));
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);
        if (result == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= AuthRules.MaxFailedLogins)
            {
                user.FailedLoginCount = 0;
                user.LockoutEndsAt = now + AuthRules.LockoutDuration;
            }
            await db.SaveChangesAsync();
            return Unauthorized(Failure(WrongCredentials));
        }

        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
        }
        await db.SaveChangesAsync();

        await legacyData.ClaimIfOwnerAsync(user);
        await SignInAsync(user);
        return UserResponse.From(user);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    // Lets the web app restore the session after a page refresh. "Nobody is signed
    // in" is a normal answer here, so it is 204 rather than an error status that
    // browsers would print as a red console error on every visit to the login page.
    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Me()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return NoContent();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? NoContent() : UserResponse.From(user);
    }

    // Changes the display name. The email is the sign-in identity and stays fixed.
    [HttpPut("profile")]
    public async Task<ActionResult<UserResponse>> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await CurrentUserAsync();
        user.Name = request.Name!.Trim();
        await db.SaveChangesAsync();
        await SignInAsync(user); // refresh the name stored in the cookie
        return UserResponse.From(user);
    }

    // Display preferences. Changing the allowance view never changes the budget.
    [HttpPut("preferences")]
    public async Task<ActionResult<UserResponse>> UpdatePreferences(PreferencesRequest request)
    {
        var user = await CurrentUserAsync();
        user.AllowancePeriod = Enum.Parse<AllowancePeriod>(request.AllowancePeriod!);
        await db.SaveChangesAsync();
        return UserResponse.From(user);
    }

    // Needs the current password. A new security stamp signs out every other
    // browser and device; this one gets a fresh cookie and stays signed in.
    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await CurrentUserAsync();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword!) == PasswordVerificationResult.Failed)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["CurrentPassword"] = ["Current password is incorrect."],
            }));
        }
        if (request.NewPassword == request.CurrentPassword)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["NewPassword"] = ["Choose a password different from your current one."],
            }));
        }

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        await db.SaveChangesAsync();
        await SignInAsync(user);
        return NoContent();
    }

    private async Task<User> CurrentUserAsync()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.Users.FirstAsync(u => u.Id == id);
    }

    private Task SignInAsync(User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(StampClaim, user.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static ProblemDetails Failure(string title) => new() { Title = title };
}
