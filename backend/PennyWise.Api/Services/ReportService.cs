using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.Services;

// The monthly report, built only from the user's saved records.
// - Spending comes from expenses only. A bill payment recorded as an expense is
//   already one of those expenses; the bills section lists payments for
//   information and is never added to spending again.
// - Savings deposits and withdrawals are shown separately; they are not spending.
// - Income is not recorded in PennyWise, so the report shows no income or net
//   cash flow rather than a made-up figure.
public class ReportService(PennyWiseDbContext db, CurrentUser currentUser, BillService bills, InsightService insights)
{
    public async Task<MonthlyReportResponse> GetMonthlyAsync(string month)
    {
        var userId = currentUser.Id;
        var (first, last) = MonthKey.Range(month);
        var prevFirst = first.AddMonths(-1);
        var prevLast = first.AddDays(-1);

        var budget = await db.Budgets.AsNoTracking().Include(b => b.CategoryLimits)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Month == month);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.ExpenseDate >= prevFirst && e.ExpenseDate <= last)
            .Select(e => new { e.Amount, e.Category, e.ExpenseDate }).ToListAsync();
        var current = expenses.Where(e => e.ExpenseDate >= first).ToList();
        var previous = expenses.Where(e => e.ExpenseDate <= prevLast).ToList();

        var totalSpent = current.Sum(e => e.Amount);
        var previousTotal = previous.Sum(e => e.Amount);

        var categories = Enum.GetValues<Category>().Select(c =>
        {
            var total = current.Where(e => e.Category == c).Sum(e => e.Amount);
            var before = previous.Where(e => e.Category == c).Sum(e => e.Amount);
            var limit = budget?.CategoryLimits.FirstOrDefault(l => l.Category == c)?.Amount;
            return new ReportCategory(c.ToString(), total, totalSpent > 0 ? Math.Round(total / totalSpent * 100, 1) : 0, limit, before, total - before);
        })
        .Where(c => c.Total > 0 || c.PreviousTotal > 0 || c.Limit is not null)
        .OrderByDescending(c => c.Total)
        .ToList();

        // Savings: deposits and withdrawals dated in this month, and each goal's balance at month end.
        var goals = await db.SavingsGoals.AsNoTracking().Include(g => g.Transactions)
            .Where(g => g.UserId == userId).OrderBy(g => g.CreatedAt).ToListAsync();
        var monthTx = goals.SelectMany(g => g.Transactions).Where(t => t.Date >= first && t.Date <= last).ToList();
        var contributions = monthTx.Where(t => t.Type == SavingsTransactionType.Contribution).Sum(t => t.Amount);
        var withdrawals = monthTx.Where(t => t.Type == SavingsTransactionType.Withdrawal).Sum(t => t.Amount);
        var reportGoals = goals
            .Where(g => DateOnly.FromDateTime(g.CreatedAt) <= last && (g.ArchivedAt is null || DateOnly.FromDateTime(g.ArchivedAt.Value) >= first))
            .Select(g =>
            {
                var saved = SavingsService.SavedUntil(g, last);
                var tx = g.Transactions.Where(t => t.Date >= first && t.Date <= last).ToList();
                return new ReportGoal(g.Name, g.TargetAmount, saved,
                    g.TargetAmount > 0 ? Math.Round(saved / g.TargetAmount * 100, 1) : 0,
                    tx.Where(t => t.Type == SavingsTransactionType.Contribution).Sum(t => t.Amount),
                    tx.Where(t => t.Type == SavingsTransactionType.Withdrawal).Sum(t => t.Amount));
            })
            .ToList();

        // Bills due this month (including archived bills, so past payments still appear).
        var dueThisMonth = await bills.OccurrencesAsync(first, last, includeArchived: true);
        var billItems = dueThisMonth
            .Select(o => new ReportBill(o.Name, o.DueDate, o.PaidAmount ?? o.Amount, o.Status, o.PaidOn, o.ExpenseId is not null))
            .ToList();
        var paid = billItems.Where(b => b.Status == "Paid").ToList();
        var unpaid = billItems.Where(b => b.Status != "Paid").ToList();

        var notes = new List<string>
        {
            "Income is not recorded in PennyWise, so this report shows no income or net cash flow.",
            "Bill payments recorded as expenses are already included in total spending; the bills section does not add them again.",
            "Savings deposits are money set aside, not spending, so they are not included in total spending.",
        };

        return new MonthlyReportResponse(
            month,
            prevFirst.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            budget?.Amount,
            totalSpent,
            current.Count,
            budget is { Amount: > 0 } ? Math.Round(totalSpent / budget.Amount * 100, 1) : null,
            budget is null ? null : budget.Amount - totalSpent,
            previousTotal,
            previousTotal > 0 ? Math.Round((totalSpent - previousTotal) / previousTotal * 100, 1) : null,
            categories,
            new ReportSavings(contributions, withdrawals, contributions - withdrawals, reportGoals),
            new ReportBills(billItems.Count, paid.Count, unpaid.Count, paid.Sum(b => b.Amount), unpaid.Sum(b => b.Amount), billItems),
            await insights.GetAsync(month),
            notes);
    }

    // CSV of the same report. Plain numbers (no currency symbol) so spreadsheets
    // can add them up; cells that start with = + - @ are prefixed with ' so a
    // spreadsheet never runs them as formulas (CSV injection).
    public static string ToCsv(MonthlyReportResponse r)
    {
        var csv = new StringBuilder();
        void Row(params object?[] cells) => csv.AppendLine(string.Join(",", cells.Select(Cell)));

        Row("PennyWise monthly report", r.Month);
        Row();
        Row("Summary", "Value");
        Row("Budget", r.Budget);
        Row("Total spent", r.TotalSpent);
        Row("Expenses recorded", r.ExpenseCount);
        Row("Budget used (%)", r.BudgetUsedPercent);
        Row("Remaining", r.Remaining);
        Row($"Total spent in {r.PreviousMonth}", r.PreviousTotalSpent);
        Row("Change from previous month (%)", r.ChangeFromPreviousPercent);
        Row();
        Row("Category", "Spent", "Share of spending (%)", "Limit", $"Spent in {r.PreviousMonth}", "Change");
        foreach (var c in r.Categories) Row(c.Category, c.Total, c.SharePercent, c.Limit, c.PreviousTotal, c.Change);
        Row();
        Row("Savings", "Value");
        Row("Deposited this month", r.Savings.Contributions);
        Row("Withdrawn this month", r.Savings.Withdrawals);
        Row("Net set aside", r.Savings.Net);
        Row();
        Row("Savings goal", "Target", "Saved at month end", "Progress (%)", "Deposited this month", "Withdrawn this month");
        foreach (var g in r.Savings.Goals) Row(g.Name, g.TargetAmount, g.SavedAtMonthEnd, g.PercentAtMonthEnd, g.ContributedThisMonth, g.WithdrawnThisMonth);
        Row();
        Row("Bill", "Due date", "Amount", "Status", "Paid on", "Recorded as expense");
        foreach (var b in r.Bills.Items) Row(b.Name, b.DueDate, b.Amount, b.Status, b.PaidOn, b.RecordedAsExpense ? "Yes" : "No");
        Row("Bills paid", r.Bills.Paid, r.Bills.PaidAmount);
        Row("Bills unpaid", r.Bills.Unpaid, r.Bills.UnpaidAmount);
        Row();
        Row("Insight", "Type", "Message");
        foreach (var i in r.Insights) Row(i.Title, i.Kind, i.Message);
        Row();
        foreach (var note in r.Notes) Row("Note", note);
        return csv.ToString();
    }

    private static string Cell(object? value)
    {
        var text = value switch
        {
            null => "",
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
        if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
