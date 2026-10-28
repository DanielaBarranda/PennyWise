namespace PennyWise.Api.Models;

public class Expense
{
    public int Id { get; set; }

    // The account that owns this record. Null only for records created before
    // accounts existed; those stay hidden until claimed by the configured owner
    // (see Auth/LegacyDataClaimer.cs).
    public int? UserId { get; set; }

    // decimal, never double: 0.1 + 0.2 must equal exactly 0.3 when adding money.
    public decimal Amount { get; set; }

    public Category Category { get; set; }

    public string Description { get; set; } = string.Empty;

    // DateOnly because an expense happens on a day, not at a time zone-dependent instant.
    public DateOnly ExpenseDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
