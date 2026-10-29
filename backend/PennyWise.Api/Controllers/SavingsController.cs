using Microsoft.AspNetCore.Mvc;
using PennyWise.Api.DTOs;
using PennyWise.Api.Services;

namespace PennyWise.Api.Controllers;

[ApiController]
[Route("api/savings")]
public class SavingsController(SavingsService savings) : ControllerBase
{
    // GET /api/savings?includeArchived=true
    [HttpGet]
    public Task<List<SavingsGoalResponse>> List(bool includeArchived = false) => savings.ListAsync(includeArchived);

    [HttpGet("{id:int}")]
    public Task<SavingsGoalResponse> Get(int id) => savings.GetAsync(id);

    [HttpPost]
    public async Task<ActionResult<SavingsGoalResponse>> Create(SavingsGoalRequest request)
    {
        var created = await savings.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<SavingsGoalResponse> Update(int id, SavingsGoalRequest request) => savings.UpdateAsync(id, request);

    [HttpPost("{id:int}/archive")]
    public Task<SavingsGoalResponse> Archive(int id) => savings.SetArchivedAsync(id, archived: true);

    [HttpPost("{id:int}/restore")]
    public Task<SavingsGoalResponse> Restore(int id) => savings.SetArchivedAsync(id, archived: false);

    [HttpGet("{id:int}/transactions")]
    public Task<List<SavingsTransactionResponse>> Transactions(int id) => savings.ListTransactionsAsync(id);

    // Deposit into or withdraw from a goal; returns the updated goal.
    [HttpPost("{id:int}/transactions")]
    public Task<SavingsGoalResponse> AddTransaction(int id, SavingsTransactionRequest request) => savings.AddTransactionAsync(id, request);
}
