using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Services;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard, AppClock clock) : ControllerBase
{
    // GET /api/dashboard/summary?month=2026-10 (defaults to the current month)
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> Summary(string? month)
    {
        if (month is not null && !MonthKey.IsValid(month))
        {
            ModelState.AddModelError(nameof(month), "Month must be in YYYY-MM format, e.g. 2026-10.");
            return ValidationProblem(ModelState);
        }
        return await dashboard.GetSummaryAsync(month ?? clock.CurrentMonth());
    }
}
