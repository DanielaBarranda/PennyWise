using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PennyWise.Api.Errors;

// Converts exceptions into ProblemDetails JSON. Unexpected errors are logged in
// full on the server but the client only gets a generic message, so stack
// traces, SQL and file paths never leak.
public sealed class AppExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationFailedException invalid)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = invalid.Message,
                },
            });
        }

        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "The request was not valid."),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong on the server. Please try again."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }
}
