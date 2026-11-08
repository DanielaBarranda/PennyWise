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
using PennyWise.Api.Services.Email;

namespace PennyWise.Api.Controllers;

// Sign-up, sign-in (password or Google, then the authenticator code when two-step
// verification is on) and sign-out. A finished sign-in sets an HttpOnly cookie
// that the browser sends automatically; JavaScript can never read it.
[ApiController]
[Route("api/auth")]
public class AuthController(
    PennyWiseDbContext db,
    IPasswordHasher<User> hasher,
    LegacyDataClaimer legacyData,
    AvatarService avatars,
    TwoFactorService twoFactor,
    GoogleTokenValidator google,
    AccountEmails emails,
    EmailLinkTokens emailTokens,
    AppClock clock,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string StampClaim = SessionCookie.StampClaim;
    private const string WrongCredentials = "Email or password is incorrect.";
    private const string NoPassword = "This account signs in with Google. Use Continue with Google.";
    private const string WrongCode = "That code is not correct. Check your authenticator app and try again.";
    private const string LoginTicket = "login";
    private const string LinkTicket = "link-google";

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
        emails.SendVerification(user);
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

        if (LockedOut(user) is { } locked) return locked;
        if (!user.HasPassword)
        {
            hasher.VerifyHashedPassword(new User(), DummyHash, request.Password!);
            return Unauthorized(Failure(NoPassword));
        }

        if (!await CheckPasswordAsync(user, request.Password!)) return Unauthorized(Failure(WrongCredentials));
        return await FinishSignInAsync(user);
    }

    // Step two when two-step verification is on: the ticket from step one plus
    // a code from the authenticator app (or a recovery code). Wrong codes count
    // towards the same lockout as wrong passwords.
    [HttpPost("login/two-factor")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<UserResponse>> LoginTwoFactor(TwoFactorLoginRequest request)
    {
        var user = await UserFromTicketAsync(request.Ticket, LoginTicket);
        if (user is null) return Unauthorized(Failure("Your sign-in has expired. Log in again."));
        if (LockedOut(user) is { } locked) return locked;

        if (!twoFactor.Verify(user, request.Code!))
        {
            await RecordFailureAsync(user);
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Code"] = [WrongCode] }));
        }

        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        await db.SaveChangesAsync();
        await legacyData.ClaimIfOwnerAsync(user);
        await SignInAsync(user);
        return UserResponse.From(user);
    }

    // The Google client ID for the sign-in button, or null when Google sign-in
    // is not set up (the button is then not shown).
    [HttpGet("options")]
    [AllowAnonymous]
    public AuthOptionsResponse Options() => new(google.ClientId, emails.IsEnabled);

    // "Forgot password?": emails a reset link if the address has an account.
    // The answer is the same either way, so nobody can use this to find out
    // which emails are registered (the email itself is sent in the background).
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        if (!emails.IsEnabled) return EmailUnavailable();
        var normalized = AuthRules.Normalize(request.Email!);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
        if (user is not null) emails.SendPasswordReset(user);
        return Accepted();
    }

    // Sets a new password from a reset link. Opening the link proves the email
    // is theirs, so it also counts as confirming it. Every device is signed out;
    // the user then logs in with the new password (and their authenticator code,
    // if two-step verification is on).
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var link = emailTokens.Read(request.Token, "reset");
        var user = link is { } l ? await db.Users.FirstOrDefaultAsync(u => u.Id == l.UserId) : null;
        if (user is null || user.SecurityStamp != link!.Value.Value)
        {
            return BadRequest(Failure("This reset link has expired or was already used. Ask for a new one."));
        }

        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        user.EmailVerifiedAt ??= clock.UtcNow;
        await db.SaveChangesAsync();
        emails.SendPasswordChanged(user);
        logger.LogInformation("Account {UserId} reset its password by email.", user.Id);
        return NoContent();
    }

    // Confirms the email from the link sent at sign-up (or by "Resend").
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<UserResponse>> VerifyEmail(VerifyEmailRequest request)
    {
        var link = emailTokens.Read(request.Token, "verify");
        var user = link is { } l ? await db.Users.FirstOrDefaultAsync(u => u.Id == l.UserId) : null;
        if (user is null || user.NormalizedEmail != link!.Value.Value)
        {
            return BadRequest(Failure("This confirmation link has expired or isn't valid. Ask for a new one."));
        }
        user.EmailVerifiedAt ??= clock.UtcNow;
        await db.SaveChangesAsync();
        return UserResponse.From(user);
    }

    // Sends the confirmation link again to the signed-in user's email.
    [HttpPost("resend-verification")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResendVerification()
    {
        if (!emails.IsEnabled) return EmailUnavailable();
        var user = await CurrentUserAsync();
        if (user.EmailVerifiedAt is not null) return Conflict(Failure("Your email is already confirmed."));
        emails.SendVerification(user);
        return Accepted();
    }

    // Continue with Google. Signs in the account connected to this Google
    // account; otherwise creates one, unless the email already has a PennyWise
    // password, in which case the password is asked for once (linkGoogle).
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<UserResponse>> Google(GoogleSignInRequest request)
    {
        var identity = await ValidateGoogleAsync(request.Credential!);
        if (identity.Result is { } failed) return failed;
        var person = identity.Value!;

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleSubject == person.Subject);
        if (user is null)
        {
            var normalized = AuthRules.Normalize(person.Email);
            user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
            if (user is not null)
            {
                if (user.GoogleSubject is not null)
                {
                    return Conflict(Failure("This email is connected to a different Google account. Log in with that one or with your password."));
                }
                return Ok(new SignInStep("linkGoogle", twoFactor.CreateTicket(user, LinkTicket, person.Subject), user.Email));
            }

            user = new User
            {
                Name = person.Name.Length > 80 ? person.Name[..80] : person.Name,
                Email = person.Email,
                NormalizedEmail = normalized,
                GoogleSubject = person.Subject,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                EmailVerifiedAt = clock.UtcNow, // Google only gives verified emails
                CreatedAt = clock.UtcNow,
            };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
            {
                return Conflict(Failure("This account was just created. Try Continue with Google again."));
            }
            logger.LogInformation("Account {UserId} was created with Google sign-in.", user.Id);
        }

        if (LockedOut(user) is { } locked) return locked;
        return await FinishSignInAsync(user);
    }

    // Connects Google to an existing password account after the password is entered.
    [HttpPost("google/link")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<UserResponse>> LinkGoogle(GoogleLinkRequest request)
    {
        var ticket = twoFactor.ReadTicket(request.Ticket, LinkTicket);
        var user = await UserFromTicketAsync(request.Ticket, LinkTicket);
        if (user is null || ticket is null) return Unauthorized(Failure("Your sign-in has expired. Try Continue with Google again."));
        if (LockedOut(user) is { } locked) return locked;

        if (!await CheckPasswordAsync(user, request.Password!))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Password"] = ["Password is incorrect."] }));
        }

        var subject = ticket.Value.Extra;
        if (await db.Users.AnyAsync(u => u.GoogleSubject == subject && u.Id != user.Id))
        {
            return Conflict(Failure("This Google account is already connected to another PennyWise account."));
        }
        user.GoogleSubject = subject;
        user.EmailVerifiedAt ??= clock.UtcNow; // Google confirmed this same email
        await db.SaveChangesAsync();
        logger.LogInformation("Account {UserId} connected Google sign-in.", user.Id);
        return await FinishSignInAsync(user);
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

    // Whether the dashboard works with daily, weekly or monthly budgets. Budgets
    // already set for other periods are kept and come back if the user switches back.
    [HttpPut("preferences")]
    public async Task<ActionResult<UserResponse>> UpdatePreferences(PreferencesRequest request)
    {
        var user = await CurrentUserAsync();
        user.BudgetPeriod = Enum.Parse<BudgetPeriod>(request.BudgetPeriod!);
        await db.SaveChangesAsync();
        return UserResponse.From(user);
    }

    // The app tour: marked done when finished or skipped; "Take the tour" resets it.
    [HttpPut("tour")]
    public async Task<ActionResult<UserResponse>> UpdateTour(TourRequest request)
    {
        var user = await CurrentUserAsync();
        user.TourCompletedAt = request.Completed!.Value ? clock.UtcNow : null;
        await db.SaveChangesAsync();
        return UserResponse.From(user);
    }

    // The signed-in user's own photo only; there is no way to ask for someone else's.
    // Served with nosniff and a locked-down CSP so the bytes can only ever be an image.
    [HttpGet("avatar")]
    public async Task<IActionResult> GetAvatar()
    {
        var avatar = await avatars.GetAsync(CurrentUserId());
        if (avatar is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=31536000, immutable"; // the URL changes with each new photo
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return File(avatar.Data, avatar.ContentType);
    }

    // The request body is the image itself (Content-Type image/jpeg, image/png or image/webp).
    [HttpPut("avatar")]
    [RequestSizeLimit(AvatarService.MaxBytes + 1024)]
    public async Task<ActionResult<UserResponse>> UploadAvatar() =>
        UserResponse.From(await avatars.SaveAsync(CurrentUserId(), Request.Body));

    [HttpDelete("avatar")]
    public async Task<ActionResult<UserResponse>> RemoveAvatar() =>
        UserResponse.From(await avatars.RemoveAsync(CurrentUserId()));

    // Needs the current password. A new security stamp signs out every other
    // browser and device; this one gets a fresh cookie and stays signed in.
    [HttpPost("change-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await CurrentUserAsync();
        if (!user.HasPassword) return BadRequest(Failure("Your account signs in with Google, so it has no password to change."));
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
        emails.SendPasswordChanged(user);
        await SignInAsync(user);
        return NoContent();
    }

    // Permanently deletes the account and everything in it: budgets, expenses,
    // savings, bills and the photo all go with the user (cascading foreign keys).
    // Every other signed-in device is signed out on its next request, because
    // its cookie no longer matches an existing account.
    [HttpPost("delete-account")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request)
    {
        var user = await CurrentUserAsync();
        if (user.HasPassword)
        {
            if (string.IsNullOrEmpty(request.Password)
                || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["Password"] = [string.IsNullOrEmpty(request.Password) ? "Enter your password to confirm." : "Password is incorrect."],
                }));
            }
        }
        else
        {
            // No password: Google must confirm it is still the same person.
            if (string.IsNullOrEmpty(request.GoogleCredential)) return BadRequest(Failure("Confirm with Google to delete your account."));
            var identity = await ValidateGoogleAsync(request.GoogleCredential);
            if (identity.Result is { } failed) return failed;
            if (identity.Value!.Subject != user.GoogleSubject)
            {
                return BadRequest(Failure("That Google account is not the one connected to PennyWise."));
            }
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        logger.LogInformation("Account {UserId} was deleted by its owner.", user.Id);
        return NoContent();
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<User> CurrentUserAsync()
    {
        var id = CurrentUserId();
        return await db.Users.FirstAsync(u => u.Id == id);
    }

    private Task SignInAsync(User user) => SessionCookie.SignInAsync(HttpContext, user);

    // The password (or Google) step is done. With two-step verification on,
    // the browser gets a ticket for the code step instead of a session.
    private async Task<ActionResult<UserResponse>> FinishSignInAsync(User user)
    {
        if (user.TwoFactorEnabled)
        {
            await db.SaveChangesAsync();
            return Ok(new SignInStep("twoFactor", twoFactor.CreateTicket(user, LoginTicket)));
        }

        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        await db.SaveChangesAsync();
        await legacyData.ClaimIfOwnerAsync(user);
        await SignInAsync(user);
        return UserResponse.From(user);
    }

    // Checks the password, counting wrong ones towards the lockout. A right one
    // does not reset the count while a code is still to come, so someone who
    // knows the password cannot keep resetting it between guesses at the code.
    private async Task<bool> CheckPasswordAsync(User user, string password)
    {
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            await RecordFailureAsync(user);
            return false;
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, password);
        }
        return true;
    }

    private async Task RecordFailureAsync(User user)
    {
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= AuthRules.MaxFailedLogins)
        {
            user.FailedLoginCount = 0;
            user.LockoutEndsAt = clock.UtcNow + AuthRules.LockoutDuration;
        }
        await db.SaveChangesAsync();
    }

    private ObjectResult? LockedOut(User user)
    {
        var now = clock.UtcNow;
        if (!(user.LockoutEndsAt > now)) return null;
        var minutes = (int)Math.Ceiling((user.LockoutEndsAt.Value - now).TotalMinutes);
        return StatusCode(StatusCodes.Status429TooManyRequests,
            Failure($"Too many wrong attempts. Try again in {minutes} minute{(minutes == 1 ? "" : "s")}."));
    }

    private ObjectResult EmailUnavailable() => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
    {
        Status = StatusCodes.Status503ServiceUnavailable,
        Title = "Email isn't set up on this server yet, so PennyWise can't send you a link.",
    });

    // The user a sign-in ticket was made for, if it is unexpired and the
    // password has not changed since.
    private async Task<User?> UserFromTicketAsync(string? ticket, string purpose)
    {
        if (twoFactor.ReadTicket(ticket, purpose) is not { } t) return null;
        return await db.Users.FirstOrDefaultAsync(u => u.Id == t.UserId && u.SecurityStamp == t.Stamp);
    }

    private async Task<ActionResult<GoogleIdentity>> ValidateGoogleAsync(string credential)
    {
        if (google.ClientId is null) return NotFound(Failure("Google sign-in is not set up."));
        try
        {
            var identity = await google.ValidateAsync(credential, HttpContext.RequestAborted);
            return identity is null ? Unauthorized(Failure("Google sign-in did not work. Please try again.")) : identity;
        }
        catch (GoogleUnavailableException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                Failure("Google sign-in is not available right now. Try again later, or log in with your password."));
        }
    }

    private static ProblemDetails Failure(string title) => new() { Title = title };
}
