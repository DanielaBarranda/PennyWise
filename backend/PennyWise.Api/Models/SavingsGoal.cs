namespace PennyWise.Api.Models;

// A savings goal. The saved amount is never stored: it is always the sum of the
// goal's contributions minus its withdrawals, so the history and the balance can
// never disagree. PennyWise records money the user has set aside; it does not
// move money between bank accounts.
public class SavingsGoal
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal TargetAmount { get; set; }

    public DateOnly? TargetDate { get; set; }

    public List<SavingsTransaction> Transactions { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Archived goals are hidden from the main lists but keep their history.
    public DateTime? ArchivedAt { get; set; }
}

public enum SavingsTransactionType
{
    Contribution,
    Withdrawal,
}

// One deposit into, or withdrawal from, a goal. Transactions are never edited,
// so the history stays an accurate record; a mistake is corrected with an
// opposite transaction.
public class SavingsTransaction
{
    public int Id { get; set; }

    public int GoalId { get; set; }

    public SavingsTransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Date { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
