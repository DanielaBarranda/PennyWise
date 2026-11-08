using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Services;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Controllers;

[ApiController]
[Route("api/budgets")]
public class BudgetsController(BudgetService budgets, CarryOverService carryOvers, AppClock clock) : ControllerBase
{
    // GET /api/budgets?month=2026-10           every budget starting in October (any period)
    // GET /api/budgets?period=Weekly&date=...  the budget for the week containing that date (or none)
    [HttpGet]
    public async Task<ActionResult<List<BudgetResponse>>> List(string? month, string? period, string? date)
    {
        if (period is null && date is null)
        {
            if (month is not null && !MonthKey.IsValid(month))
            {
                ModelState.AddModelError(nameof(month), "Month must be in YYYY-MM format, e.g. 2026-10.");
                return ValidationProblem(ModelState);
            }
            return await budgets.ListAsync(month);
        }
        if (PeriodQuery.Resolve(ModelState, null, period, date, clock.Today()) is not { } window) return ValidationProblem(ModelState);
        return await budgets.ListAsync(null, window);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BudgetResponse>> Get(int id) => await budgets.GetAsync(id);

    [HttpPost]
    public async Task<ActionResult<BudgetResponse>> Create(BudgetRequest request)
    {
        var created = await budgets.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BudgetResponse>> Update(int id, BudgetRequest request) =>
        await budgets.UpdateAsync(id, request);

    // What to do with the money left in the period before this budget's period:
    // add it to this budget, move it into a savings goal, or leave it.
    [HttpPost("{id:int}/carryover")]
    public async Task<ActionResult<CarryOverResponse>> CarryOver(int id, CarryOverRequest request) =>
        await carryOvers.ApplyAsync(id, request);

    // Removes the budget so the period has none; expenses stay as they are.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await budgets.DeleteAsync(id);
        return NoContent();
    }
}
