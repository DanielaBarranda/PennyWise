using System.ComponentModel.DataAnnotations;
using PennyWise.Api.Validation;

namespace PennyWise.Api.DTOs;

public class BillRequest : IValidatableObject
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(80, ErrorMessage = "Name must be 80 characters or fewer.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Money]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    [CategoryName]
    public string? Category { get; set; }

    [Required(ErrorMessage = "Choose how often the bill repeats.")]
    [AllowedValues("OneTime", "Weekly", "Monthly", "Yearly", ErrorMessage = "Frequency must be OneTime, Weekly, Monthly or Yearly.")]
    public string? Frequency { get; set; }

    [Required(ErrorMessage = "Due date is required.")]
    [ValidDate]
    public DateOnly? FirstDueDate { get; set; }

    [ValidDate]
    public DateOnly? EndDate { get; set; }

    [StringLength(200, ErrorMessage = "Notes must be 200 characters or fewer.")]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDate is not null && Frequency == "OneTime")
        {
            yield return new ValidationResult("A one-time bill has no end date.", [nameof(EndDate)]);
        }
        else if (EndDate < FirstDueDate)
        {
            yield return new ValidationResult("End date must be on or after the first due date.", [nameof(EndDate)]);
        }
    }
}

public class BillPaymentRequest
{
    [Required(ErrorMessage = "Due date is required.")]
    [ValidDate]
    public DateOnly? DueDate { get; set; }

    // Defaults to today.
    [ValidDate]
    public DateOnly? PaidOn { get; set; }

    // True: also record the payment as one expense (linked to this payment).
    // False: the payment was already recorded as an expense, or should not be.
    public bool RecordExpense { get; set; } = true;
}

public record BillResponse(
    int Id,
    string Name,
    decimal Amount,
    string Category,
    string Frequency,
    DateOnly FirstDueDate,
    DateOnly? EndDate,
    string? Notes,
    // The earliest unpaid due date (overdue or upcoming), or null if none remain.
    DateOnly? NextDueDate,
    string? NextStatus,
    bool Archived);

// One due date of a bill and whether it has been paid.
public record BillOccurrenceResponse(
    int BillId,
    string Name,
    decimal Amount,
    string Category,
    string Frequency,
    DateOnly DueDate,
    string Status, // Paid | Overdue | DueSoon | Upcoming
    int DaysUntilDue,
    DateOnly? PaidOn,
    decimal? PaidAmount,
    int? ExpenseId);
