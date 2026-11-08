namespace PennyWise.Api.DTOs;

// Everything on the monthly report. The CSV export is generated from this same
// object, so the file always matches the screen.
public record MonthlyReportResponse(
    string Month,
    string PreviousMonth,
    decimal? Budget,
    decimal TotalSpent,
    int ExpenseCount,
    decimal? BudgetUsedPercent,
    decimal? Remaining,
    decimal PreviousTotalSpent,
    decimal? ChangeFromPreviousPercent,
    List<ReportCategory> Categories,
    ReportSavings Savings,
    ReportBills Bills,
    List<ReportPeriodBudget> PeriodBudgets, // weekly and daily budgets starting this month
    List<ReportTransaction> Transactions,   // every expense and savings move dated this month, oldest first
    List<InsightResponse> Insights,
    List<string> Notes);

// Kind: Expense | SavingsDeposit | SavingsWithdrawal. Category is the expense
// category, or the goal's name for a savings move.
// PaidFrom: for an expense paid from savings, the goal or fund's name (not counted in the budget).
// FromOutside: for a deposit, true when it did not come from a budget.
public record ReportTransaction(DateOnly Date, string Kind, string Description, string Category, decimal Amount,
    string? PaidFrom = null, bool FromOutside = false);

public record ReportCategory(string Category, decimal Total, decimal SharePercent, decimal? Limit, decimal PreviousTotal, decimal Change);

// FromBudget: the part of Net that came out of budgets (deposits from a budget minus plain withdrawals, at least 0).
// SpentFromSavings: expenses paid with money taken out of savings (also inside Withdrawals).
public record ReportSavings(decimal Contributions, decimal Withdrawals, decimal Net, decimal FromBudget, decimal SpentFromSavings, List<ReportGoal> Goals);

public record ReportGoal(string Name, decimal? TargetAmount, decimal SavedAtMonthEnd, decimal PercentAtMonthEnd, decimal ContributedThisMonth, decimal WithdrawnThisMonth);

// Left = Amount - Spent - Saved (moved into savings within the budget's dates)
//        - CarriedOut (added to the next period's budget). Amount already includes CarriedIn.
public record ReportPeriodBudget(string Period, DateOnly Start, DateOnly End, decimal Amount, decimal Spent, decimal Saved,
    decimal CarriedIn, decimal CarriedOut, decimal Left);

public record ReportBills(int Due, int Paid, int Unpaid, decimal PaidAmount, decimal UnpaidAmount, List<ReportBill> Items);

public record ReportBill(string Name, DateOnly DueDate, decimal Amount, string Status, DateOnly? PaidOn, bool RecordedAsExpense);
