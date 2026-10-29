using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Controllers;
using PennyWise.Api.Data;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;
using PennyWise.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection("PennyWise");

// ---------- Database ----------
// The path comes from configuration: "pennywise.db" next to the project when
// developing, /var/lib/pennywise/pennywise.db on EC2 (appsettings.Production.json).
// A relative path is anchored to the app folder so it never depends on which
// directory the API was started from.
var connection = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("PennyWise")
    ?? throw new InvalidOperationException("Connection string 'PennyWise' is missing."));
connection.DataSource = Path.GetFullPath(connection.DataSource, builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(connection.DataSource)!);

builder.Services.AddDbContext<PennyWiseDbContext>(options => options.UseSqlite(connection.ToString()));

// ---------- Application services ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<LegacyDataClaimer>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<SavingsService>();
builder.Services.AddScoped<AvatarService>();
builder.Services.AddScoped<BillService>();
builder.Services.AddScoped<AllowanceService>();
builder.Services.AddScoped<InsightService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ---------- Sign-in cookie ----------
// The keys that sign the cookie are kept on disk in production, so signing in
// survives API restarts and deployments (otherwise everyone would be signed out).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("PennyWise");
if (settings["KeysPath"] is { Length: > 0 } keysPath)
{
    dataProtection.PersistKeysToFileSystem(Directory.CreateDirectory(keysPath));
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "pennywise_session";
        options.Cookie.HttpOnly = true;                 // JavaScript can never read it
        options.Cookie.SameSite = SameSiteMode.Strict;  // never sent with requests from other sites
        // Production is HTTPS only; local development runs on http://localhost.
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
            "Please log in to continue.");
        options.Events.OnRedirectToAccessDenied = context => WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden,
            "You do not have access to this.");

        // Reject cookies for accounts that no longer exist or whose password has
        // changed since the cookie was issued.
        options.Events.OnValidatePrincipal = async context =>
        {
            var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = context.Principal?.FindFirstValue(AuthController.StampClaim);
            var db = context.HttpContext.RequestServices.GetRequiredService<PennyWiseDbContext>();
            var valid = int.TryParse(id, out var userId)
                && await db.Users.AnyAsync(u => u.Id == userId && u.SecurityStamp == stamp);
            if (!valid)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Every endpoint requires a signed-in user unless it is marked [AllowAnonymous]
// (sign-up, sign-in, sign-out and the health check). New endpoints are therefore
// protected by default.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ---------- Brute-force protection ----------
var authAttemptsPerMinute = settings.GetValue("AuthAttemptsPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimits.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    options.OnRejected = (context, _) => new ValueTask(WriteProblemAsync(context.HttpContext,
        StatusCodes.Status429TooManyRequests, "Too many attempts. Wait a minute and try again."));
});

// Behind Nginx, the visitor's real IP and https scheme arrive in X-Forwarded-*
// headers. They are only trusted from the frontend instance (appsettings.Production.json).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    foreach (var proxy in (settings["TrustedProxies"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    }
});

// ---------- Web API ----------
builder.Services.AddControllers(options =>
{
    // Request DTOs mark required fields explicitly with [Required]; this stops
    // ASP.NET adding a confusing extra "The request field is required." error.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
}).AddJsonOptions(options =>
{
    // Malformed JSON gets a short "not valid" message instead of parser
    // details that mention internal C# type names.
    options.AllowInputFormatterExceptionMessages = false;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddOpenApi();

// No CORS setup is needed (or wanted): the browser always talks to the same
// origin (Vite locally, Nginx on AWS), and that server forwards /api to this API.

var app = builder.Build();

// Create the database file and apply any pending migrations at startup.
// Migrations only add what is missing; they never wipe existing data.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PennyWiseDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("Using SQLite database at {Path}", connection.DataSource);
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(); // Unknown routes also return ProblemDetails JSON.
app.UseMiddleware<ClientHeaderMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous(); // API description at /openapi/v1.json (development only)
}

app.MapControllers();

app.Run();

static Task WriteProblemAsync(HttpContext context, int status, string title)
{
    context.Response.StatusCode = status;
    return context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
    {
        HttpContext = context,
        ProblemDetails = new ProblemDetails { Status = status, Title = title },
    }).AsTask();
}

// Lets the integration test project start this API in memory.
public partial class Program;
