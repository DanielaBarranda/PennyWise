using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Services;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Controllers;

// [ApiController] validates request bodies automatically: an invalid body never
// reaches these methods and gets a 400 response with field-by-field errors.
[ApiController]
[Route("api/expenses")]
public class ExpensesController(ExpenseService expenses) : ControllerBase
{
    // GET /api/expenses?month=2026-10&category=Food
    [HttpGet]
    public async Task<ActionResult<List<ExpenseResponse>>> List(string? month, string? category)
    {
        if (month is not null && !MonthKey.IsValid(month))
            ModelState.AddModelError(nameof(month), "Month must be in YYYY-MM format, e.g. 2026-10.");

        Category? parsedCategory = null;
        if (category is not null)
        {
            if (Enum.GetNames<Category>().Contains(category)) parsedCategory = Enum.Parse<Category>(category);
            else ModelState.AddModelError(nameof(category), $"Category must be one of: {string.Join(", ", Enum.GetNames<Category>())}.");
        }

        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return await expenses.ListAsync(month, parsedCategory);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpenseResponse>> Get(int id) => await expenses.GetAsync(id);

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> Create(ExpenseRequest request)
    {
        var created = await expenses.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ExpenseResponse>> Update(int id, ExpenseRequest request) =>
        await expenses.UpdateAsync(id, request);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await expenses.DeleteAsync(id);
        return NoContent();
    }
}
