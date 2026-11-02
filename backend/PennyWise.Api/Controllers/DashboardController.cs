using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Services;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard, AppClock clock) : ControllerBase
{
    // GET /api/dashboard/summary?month=2026-10, or ?period=Weekly&date=2026-11-04
    // (see PeriodQuery; defaults to the current month)
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> Summary(string? month, string? period, string? date)
    {
        if (PeriodQuery.Resolve(ModelState, month, period, date, clock.Today()) is not { } window) return ValidationProblem(ModelState);
        return await dashboard.GetSummaryAsync(window);
    }
}
