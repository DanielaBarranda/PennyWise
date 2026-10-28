using System.ComponentModel.DataAnnotations;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.DTOs;

// DTOs (Data Transfer Objects) are the JSON shapes the API accepts and returns.
// Keeping them separate from the database models means clients can never set
// fields like Id or CreatedAt themselves.

public class ExpenseRequest : IValidatableObject
{
    private static readonly DateOnly MinDate = new(2000, 1, 1);
    private static readonly DateOnly MaxDate = new(2099, 12, 31);

    // Nullable so [Required] can tell "missing" apart from 0.
    [Required(ErrorMessage = "Amount is required.")]
    [Money]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    [CategoryName]
    public string? Category { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(200, ErrorMessage = "Description must be 200 characters or fewer.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Expense date is required.")]
    public DateOnly? ExpenseDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpenseDate < MinDate || ExpenseDate > MaxDate)
        {
            yield return new ValidationResult(
                "Expense date must be between the years 2000 and 2099.", [nameof(ExpenseDate)]);
        }
    }
}

public record ExpenseResponse(
    int Id,
    decimal Amount,
    string Category,
    string Description,
    DateOnly ExpenseDate,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ExpenseResponse From(Expense e) =>
        new(e.Id, e.Amount, e.Category.ToString(), e.Description, e.ExpenseDate, e.CreatedAt, e.UpdatedAt);
}
