using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;

namespace PennyWise.Api.Controllers;

// Turning two-step verification on and off for the signed-in user. Once it is
// on, signing in needs a 6-digit code from an authenticator app as well as the
// password (or Google).
[ApiController]
[Route("api/auth/two-factor")]
[EnableRateLimiting(RateLimits.Auth)]
public class TwoFactorController(PennyWiseDbContext db, TwoFactorService twoFactor, ILogger<TwoFactorController> logger) : ControllerBase
{
    private const string WrongCode = "That code is not correct. Check your authenticator app and try again.";

    // A new secret for the authenticator app (as a QR code link and as text).
    // Nothing changes for signing in until a code from it is confirmed.
    [HttpPost("setup")]
    public async Task<ActionResult<TwoFactorSetupResponse>> Setup()
    {
        var user = await CurrentUserAsync();
        if (user.TwoFactorEnabled) return Conflict(new ProblemDetails { Title = "Two-step verification is already on." });

        var (key, uri) = twoFactor.BeginSetup(user);
        await db.SaveChangesAsync();
        return new TwoFactorSetupResponse(key, uri);
    }

    // Confirms the app is set up and turns two-step verification on. Every other
    // signed-in device is signed out; this one gets a fresh cookie.
    [HttpPost("enable")]
    public async Task<ActionResult<RecoveryCodesResponse>> Enable(TwoFactorCodeRequest request)
    {
        var user = await CurrentUserAsync();
        if (user.TwoFactorEnabled) return Conflict(new ProblemDetails { Title = "Two-step verification is already on." });
        if (user.TwoFactorPendingSecret is null) return BadRequest(new ProblemDetails { Title = "Start the setup again." });

        var codes = twoFactor.TryEnable(user, request.Code!);
        if (codes is null) return CodeProblem();

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        await SessionCookie.SignInAsync(HttpContext, user);
        logger.LogInformation("Account {UserId} turned on two-step verification.", user.Id);
        return new RecoveryCodesResponse(codes);
    }

    // Needs a current code (or a recovery code), so someone using a browser left
    // signed in cannot quietly switch it off.
    [HttpPost("disable")]
    public async Task<ActionResult<UserResponse>> Disable(TwoFactorCodeRequest request)
    {
        var user = await CurrentUserAsync();
        if (!user.TwoFactorEnabled) return UserResponse.From(user);
        if (!twoFactor.Verify(user, request.Code!)) return CodeProblem();

        twoFactor.Disable(user);
        await db.SaveChangesAsync();
        logger.LogInformation("Account {UserId} turned off two-step verification.", user.Id);
        return UserResponse.From(user);
    }

    // New recovery codes; the old ones stop working.
    [HttpPost("recovery-codes")]
    public async Task<ActionResult<RecoveryCodesResponse>> NewRecoveryCodes(TwoFactorCodeRequest request)
    {
        var user = await CurrentUserAsync();
        if (!user.TwoFactorEnabled) return BadRequest(new ProblemDetails { Title = "Two-step verification is off." });
        if (!twoFactor.Verify(user, request.Code!)) return CodeProblem();

        var codes = twoFactor.NewRecoveryCodes(user);
        await db.SaveChangesAsync();
        return new RecoveryCodesResponse(codes);
    }

    private ActionResult CodeProblem() =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Code"] = [WrongCode] }));

    private async Task<User> CurrentUserAsync()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await db.Users.FirstAsync(u => u.Id == id);
    }
}
