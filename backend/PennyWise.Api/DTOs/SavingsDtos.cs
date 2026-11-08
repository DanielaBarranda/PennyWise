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

    // Optional: without a target this is a plain fund (e.g. "Wallet") that only keeps a balance.
    [Money]
    public decimal? TargetAmount { get; set; }

    [ValidDate]
    public DateOnly? TargetDate { get; set; }

    // Create only, optional: money already in it. It never comes out of a budget.
    [Money]
    public decimal? StartingAmount { get; set; }
}

public class SavingsTransactionRequest : IValidatableObject
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

    // Deposits: true when the money is not from a budget (already owned, a gift).
    public bool FromOutside { get; set; }

    // Withdrawals: also record the money as an expense in Category, paid from savings.
    public bool RecordExpense { get; set; }

    [AllowedValues("Food", "Transportation", "School", "Bills", "Entertainment", "Others", null, ErrorMessage = "Choose a valid category.")]
    public string? Category { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RecordExpense && Type == "Withdrawal" && Category is null)
        {
            yield return new ValidationResult("Choose the expense category.", [nameof(Category)]);
        }
    }
}

public record SavingsGoalResponse(
    int Id,
    string Name,
    string? Description,
    decimal? TargetAmount,   // null for a fund without a goal
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

public record SavingsTransactionResponse(int Id, string Type, decimal Amount, DateOnly Date, string? Note, bool FromOutside, int? ExpenseId, DateTime CreatedAt);
