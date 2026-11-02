using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PennyWise.Api.Models;

namespace PennyWise.Api.Auth;

// Issues the HttpOnly sign-in cookie. It carries the security stamp, so the
// cookie stops working when the stamp changes (see Program.cs).
public static class SessionCookie
{
    public const string StampClaim = "pennywise:stamp";

    public static Task SignInAsync(HttpContext context, User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(StampClaim, user.SecurityStamp),
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
