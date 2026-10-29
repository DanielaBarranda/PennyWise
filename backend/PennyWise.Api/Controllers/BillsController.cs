using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Services;

namespace PennyWise.Api.Controllers;

[ApiController]
[Route("api/bills")]
public class BillsController(BillService bills, AppClock clock) : ControllerBase
{
    [HttpGet]
    public Task<List<BillResponse>> List(bool includeArchived = false) => bills.ListAsync(includeArchived);

    [HttpGet("{id:int}")]
    public Task<BillResponse> Get(int id) => bills.GetAsync(id);

    // GET /api/bills/occurrences?from=2026-10-01&to=2026-11-30 (default: the
    // current month through 60 days from today).
    [HttpGet("occurrences")]
    public async Task<ActionResult<List<BillOccurrenceResponse>>> Occurrences(DateOnly? from, DateOnly? to)
    {
        var today = clock.Today();
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today.AddDays(60);
        if (end < start || end.DayNumber - start.DayNumber > 800)
        {
            ModelState.AddModelError(nameof(to), "Choose a date range of up to about two years, with 'to' after 'from'.");
            return ValidationProblem(ModelState);
        }
        return await bills.OccurrencesAsync(start, end);
    }

    [HttpPost]
    public async Task<ActionResult<BillResponse>> Create(BillRequest request)
    {
        var created = await bills.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<BillResponse> Update(int id, BillRequest request) => bills.UpdateAsync(id, request);

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        await bills.SetArchivedAsync(id, archived: true);
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        await bills.SetArchivedAsync(id, archived: false);
        return NoContent();
    }

    // Mark one due date as paid (optionally recording it as one expense).
    [HttpPost("{id:int}/payments")]
    public Task<BillOccurrenceResponse> MarkPaid(int id, BillPaymentRequest request) => bills.MarkPaidAsync(id, request);

    // Undo: DELETE /api/bills/5/payments/2026-10-15
    [HttpDelete("{id:int}/payments/{dueDate}")]
    public async Task<IActionResult> UnmarkPaid(int id, DateOnly dueDate)
    {
        await bills.UnmarkPaidAsync(id, dueDate);
        return NoContent();
    }
}
