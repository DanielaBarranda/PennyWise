using Microsoft.AspNetCore.Mvc;

namespace PennyWise.Api.Auth;

// CSRF protection on top of SameSite=Strict cookies: every request that changes
// data must carry the X-PennyWise-Client header. The PennyWise web app always
// sends it; a form or script on another website cannot add a custom header to a
// request here without a CORS permission, which this API never grants.
public sealed class ClientHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-PennyWise-Client";

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        var changesData = !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method));

        if (changesData && context.Request.Path.StartsWithSegments("/api") && !context.Request.Headers.ContainsKey(HeaderName))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "This request was blocked because it did not come from the PennyWise app.",
                },
            });
            return;
        }

        await next(context);
    }
}
