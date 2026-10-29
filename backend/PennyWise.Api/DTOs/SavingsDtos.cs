using System.ComponentModel.DataAnnotations;
using PennyWise.Api.Validation;

namespace PennyWise.Api.DTOs;

public class SavingsGoalRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, ErrorMessage = "Name must be 80 characters or fewer.")]
    public string? Name { get; set; }

    [StringLength(200, ErrorMessage = "Description must be 200 characters or fewer.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Target amount is required.")]
    [Money]
    public decimal? TargetAmount { get; set; }

    [ValidDate]
    public DateOnly? TargetDate { get; set; }
}

public class SavingsTransactionRequest
{
    [Required(ErrorMessage = "Choose deposit or withdrawal.")]
    [AllowedValues("Contribution", "Withdrawal", ErrorMessage = "Type must be Contribution or Withdrawal.")]
    public string? Type { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Money]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Date is required.")]
    [ValidDate]
    public DateOnly? Date { get; set; }

    [StringLength(120, ErrorMessage = "Note must be 120 characters or fewer.")]
    public string? Note { get; set; }
}

public record SavingsGoalResponse(
    int Id,
    string Name,
    string? Description,
    decimal TargetAmount,
    decimal SavedAmount,
    decimal RemainingAmount,
    decimal PercentSaved,
    DateOnly? TargetDate,
    int? DaysLeft,
    // What would be saved by today if saving evenly from the start date to the
    // target date; null without a target date.
    decimal? ExpectedByToday,
    string Status, // Active | Completed | Archived
    DateTime CreatedAt);

public record SavingsTransactionResponse(int Id, string Type, decimal Amount, DateOnly Date, string? Note, DateTime CreatedAt);
