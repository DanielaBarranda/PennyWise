namespace PennyWise.Api.Models;

// What the user chose to do with the money left in a finished budget period,
// recorded once per budget (FromBudgetId is unique), so the question is never
// asked twice and the money is never counted twice.
//   AddToBudget:   the next period's budget (ToBudgetId) was raised by Amount,
//                  and the finished period counts Amount as used.
//   MoveToSavings: a savings deposit (SavingsTransactionId) dated on the finished
//                  period's last day; savings already come out of that period.
//   Keep:          nothing moves; the money stays as "left" in the finished period.
public class BudgetCarryOver
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int FromBudgetId { get; set; }

    public int ToBudgetId { get; set; }

    public CarryOverAction Action { get; set; }

    public decimal Amount { get; set; }

    // AddToBudget only: the category whose limit was raised too, if one was chosen.
    public Category? Category { get; set; }

    // MoveToSavings only.
    public int? SavingsTransactionId { get; set; }

    public DateTime CreatedAt { get; set; }
}

public enum CarryOverAction
{
    AddToBudget,
    MoveToSavings,
    Keep,
}
