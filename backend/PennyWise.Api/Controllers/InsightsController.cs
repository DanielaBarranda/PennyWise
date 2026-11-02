using System.Text;
using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Services;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Controllers;

// Insights and allowance take ?month=YYYY-MM or ?period=Weekly&date=YYYY-MM-DD
// (see PeriodQuery); reports take ?month=YYYY-MM. Default: today, Manila time.
[ApiController]
[Route("api")]
public class InsightsController(InsightService insights, AllowanceService allowance, ReportService reports, AppClock clock) : ControllerBase
{
    [HttpGet("insights")]
    public async Task<ActionResult<List<InsightResponse>>> Insights(string? month, string? period, string? date)
    {
        if (PeriodQuery.Resolve(ModelState, month, period, date, clock.Today()) is not { } window) return ValidationProblem(ModelState);
        return await insights.GetAsync(window);
    }

    // GET /api/allowance?month=2026-10, or ?period=Weekly&date=2026-11-04
    [HttpGet("allowance")]
    public async Task<ActionResult<AllowanceResponse>> Allowance(string? month, string? period, string? date)
    {
        if (PeriodQuery.Resolve(ModelState, month, period, date, clock.Today()) is not { } window) return ValidationProblem(ModelState);
        return await allowance.CalculateAsync(window);
    }

    [HttpGet("reports/monthly")]
    public async Task<ActionResult<MonthlyReportResponse>> Monthly(string? month)
    {
        if (InvalidMonth(month)) return ValidationProblem(ModelState);
        return await reports.GetMonthlyAsync(month ?? clock.CurrentMonth());
    }

    // The same report as a CSV download (UTF-8 with BOM so Excel shows ₱ and accents correctly).
    [HttpGet("reports/monthly.csv")]
    public async Task<IActionResult> MonthlyCsv(string? month)
    {
        if (InvalidMonth(month)) return ValidationProblem(ModelState);
        var key = month ?? clock.CurrentMonth();
        var csv = ReportService.ToCsv(await reports.GetMonthlyAsync(key));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"pennywise-report-{key}.csv");
    }

    private bool InvalidMonth(string? month)
    {
        if (month is null || MonthKey.IsValid(month)) return false;
        ModelState.AddModelError(nameof(month), "Month must be in YYYY-MM format, e.g. 2026-10.");
        return true;
    }
}
