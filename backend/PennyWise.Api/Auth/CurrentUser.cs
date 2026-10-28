using System.Security.Claims;

namespace PennyWise.Api.Auth;

// The signed-in user's id, taken from the validated sign-in cookie on the server.
// Services use this for every query; an id sent by the browser is never trusted.
public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    public int Id =>
        int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            // Unreachable for API endpoints: the authorization policy rejects
            // anonymous requests before any service code runs.
            : throw new InvalidOperationException("No signed-in user for this request.");
}
