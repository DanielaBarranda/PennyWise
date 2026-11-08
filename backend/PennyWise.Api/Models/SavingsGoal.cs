namespace PennyWise.Api.Models;

// A savings goal, or (without a target) a plain fund such as "Wallet" or
// "Envelope" that the user names. The saved amount is never stored: it is always the sum of the
// goal's contributions minus its withdrawals, so the history and the balance can
// never disagree. PennyWise records money the user has set aside; it does not
// move money between bank accounts.
public class SavingsGoal
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Null for a fund with no goal: it only keeps a balance.
    public decimal? TargetAmount { get; set; }

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

    // Deposits only: true when the money did not come out of a budget (money the
    // user already had, a gift, an allowance received). Those deposits never
    // change a budget; the others come out of the budget for their date.
    public bool FromOutside { get; set; }

    // Withdrawals only: the expense this money paid for. That expense is paid
    // from savings, so it is not counted against any budget or category limit.
    public int? ExpenseId { get; set; }

    public DateTime CreatedAt { get; set; }
}
