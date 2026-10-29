namespace PennyWise.Api.Models;

public enum BillFrequency
{
    OneTime,
    Weekly,
    Monthly,
    Yearly,
}

// A bill and its schedule. Future due dates are calculated from FirstDueDate and
// Frequency (see Services/BillSchedule.cs) instead of being stored, so recurring
// bills can never produce duplicate entries.
public class Bill
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Category Category { get; set; }

    public BillFrequency Frequency { get; set; }

    public DateOnly FirstDueDate { get; set; }

    // Optional last date for a recurring bill (e.g. a 12-month plan).
    public DateOnly? EndDate { get; set; }

    public string? Notes { get; set; }

    public List<BillPayment> Payments { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ArchivedAt { get; set; }
}

// "The bill due on DueDate has been paid." At most one per bill and due date
// (unique index), so the same occurrence can never be paid twice. When the
// payment was also recorded as an expense, ExpenseId links to it, so it is
// counted once in spending and reports.
public class BillPayment
{
    public int Id { get; set; }

    public int BillId { get; set; }

    public DateOnly DueDate { get; set; }

    public DateOnly PaidOn { get; set; }

    public decimal Amount { get; set; }

    public int? ExpenseId { get; set; }

    public Expense? Expense { get; set; }

    public DateTime CreatedAt { get; set; }
}
