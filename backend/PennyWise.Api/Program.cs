using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Data;
using PennyWise.Api.Errors;
using PennyWise.Api.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<DashboardService>();

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

// No CORS setup is needed: the browser always talks to the same origin
// (Vite locally, Nginx on AWS), and that server forwards /api to this API.

var app = builder.Build();

// Create the database file and apply any pending migrations at startup.
// Migrations only add what is missing; they never wipe existing data.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PennyWiseDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("Using SQLite database at {Path}", connection.DataSource);
}

app.UseExceptionHandler();
app.UseStatusCodePages(); // Unknown routes also return ProblemDetails JSON.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // API description at /openapi/v1.json (development only)
}

app.MapControllers();

app.Run();

// Lets the integration test project start this API in memory.
public partial class Program;
