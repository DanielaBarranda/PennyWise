using System.ComponentModel.DataAnnotations;

namespace PennyWise.Api.DTOs;

// Money left in the finished period just before the shown one, not decided yet.
public record LeftoverOffer(int FromBudgetId, string Period, DateOnly Start, DateOnly End, decimal Amount);

// POST /api/budgets/{id}/carryover, where {id} is the budget the money would go
// to. The amount is never sent: the server works out what was left.
public class CarryOverRequest
{
    [Required(ErrorMessage = "Choose what to do with the money left.")]
    [AllowedValues("AddToBudget", "MoveToSavings", "Keep", ErrorMessage = "Action must be AddToBudget, MoveToSavings or Keep.")]
    public string? Action { get; set; }

    // AddToBudget: optional, a category that has a limit in the receiving budget.
    [AllowedValues("Food", "Transportation", "School", "Bills", "Entertainment", "Others", null, ErrorMessage = "Choose a valid category.")]
    public string? Category { get; set; }

    // MoveToSavings: required, one of the user's active goals.
    public int? GoalId { get; set; }
}

public record CarryOverResponse(string Action, decimal Amount, DateOnly FromStart, DateOnly FromEnd, string? Category, string? GoalName);
