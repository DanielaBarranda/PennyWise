namespace PennyWise.Api.Services;

// EC2 servers run on UTC, but a Filipino user's "current month" starts at
// midnight Manila time (UTC+8). This decides "today" in the configured zone.
public sealed class AppClock(IConfiguration configuration, TimeProvider timeProvider)
{
    private readonly TimeZoneInfo _zone =
        TimeZoneInfo.FindSystemTimeZoneById(configuration["PennyWise:TimeZone"] ?? "Asia/Manila");

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public string CurrentMonth() => Today().ToString("yyyy-MM");

    // Today's date on the user's calendar (Manila), used for due dates and days left.
    public DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _zone));
}
