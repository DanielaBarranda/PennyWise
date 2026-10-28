using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Services;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Controllers;

[ApiController]
[Route("api/budgets")]
public class BudgetsController(BudgetService budgets) : ControllerBase
{
    // GET /api/budgets?month=2026-10
    [HttpGet]
    public async Task<ActionResult<List<BudgetResponse>>> List(string? month)
    {
        if (month is not null && !MonthKey.IsValid(month))
        {
            ModelState.AddModelError(nameof(month), "Month must be in YYYY-MM format, e.g. 2026-10.");
            return ValidationProblem(ModelState);
        }
        return await budgets.ListAsync(month);
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
}
