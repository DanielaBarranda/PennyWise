using Microsoft.EntityFrameworkCore;
using PennyWise.Api.Auth;
using PennyWise.Api.Data;
using PennyWise.Api.DTOs;
using PennyWise.Api.Errors;
using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// Bills and their due dates for the signed-in user.
//
// Only due dates on or after the day a bill was added count as owed, so adding a
// monthly bill whose first due date is in the past does not invent months of
// "overdue" history.
//
// Paying a bill stores one BillPayment per due date. It can also create exactly
// one expense, linked by ExpenseId, so the payment is counted once in spending.
public class BillService(PennyWiseDbContext db, AppClock clock, CurrentUser currentUser)
{
    public const int DueSoonDays = 7;

    public async Task<List<BillResponse>> ListAsync(bool includeArchived)
    {
        var bills = await LoadBillsAsync(includeArchived);
        var today = clock.Today();
        return bills.Select(b =>
        {
            var next = OccurrencesFor(b, StartOfOwed(b), today.AddYears(2), today).FirstOrDefault(o => o.Status != "Paid");
            return new BillResponse(b.Id, b.Name, b.Amount, b.Category.ToString(), b.Frequency.ToString(), b.FirstDueDate,
                b.EndDate, b.Notes, next?.DueDate, next?.Status, b.ArchivedAt is not null);
        })
        .OrderBy(b => b.Archived).ThenBy(b => b.NextDueDate ?? DateOnly.MaxValue)
        .ToList();
    }

    public async Task<BillResponse> GetAsync(int id)
    {
        var bill = await FindAsync(id);
        var today = clock.Today();
        var next = OccurrencesFor(bill, StartOfOwed(bill), today.AddYears(2), today).FirstOrDefault(o => o.Status != "Paid");
        return new BillResponse(bill.Id, bill.Name, bill.Amount, bill.Category.ToString(), bill.Frequency.ToString(),
            bill.FirstDueDate, bill.EndDate, bill.Notes, next?.DueDate, next?.Status, bill.ArchivedAt is not null);
    }

    // Due dates between from and to (inclusive), with status. Archived bills are
    // left out unless asked for (reports include them so past payments still show).
    public async Task<List<BillOccurrenceResponse>> OccurrencesAsync(DateOnly from, DateOnly to, bool includeArchived = false)
    {
        var bills = await LoadBillsAsync(includeArchived);
        var today = clock.Today();
        return bills
            .SelectMany(b => OccurrencesFor(b, Max(from, StartOfOwed(b)), to, today))
            .OrderBy(o => o.DueDate).ThenBy(o => o.Name)
            .ToList();
    }

    public async Task<BillResponse> CreateAsync(BillRequest request)
    {
        var now = clock.UtcNow;
        var bill = new Bill { UserId = currentUser.Id, CreatedAt = now };
        Apply(bill, request, now);
        db.Bills.Add(bill);
        await db.SaveChangesAsync();
        return await GetAsync(bill.Id);
    }

    public async Task<BillResponse> UpdateAsync(int id, BillRequest request)
    {
        var bill = await FindAsync(id);
        Apply(bill, request, clock.UtcNow);
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    // Archiving stops future reminders but keeps the bill's payment history.
    public async Task SetArchivedAsync(int id, bool archived)
    {
        var bill = await FindAsync(id);
        bill.ArchivedAt = archived ? clock.UtcNow : null;
        bill.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<BillOccurrenceResponse> MarkPaidAsync(int billId, BillPaymentRequest request)
    {
        var bill = await FindAsync(billId);
        var dueDate = request.DueDate!.Value;
        var today = clock.Today();
        var paidOn = request.PaidOn ?? today;

        if (!BillSchedule.IsOccurrence(bill, dueDate))
        {
            throw new ValidationFailedException(nameof(request.DueDate), "This bill is not due on that date.");
        }
        if (paidOn > today)
        {
            throw new ValidationFailedException(nameof(request.PaidOn), "Payment date can't be in the future.");
        }
        if (bill.Payments.Any(p => p.DueDate == dueDate))
        {
            throw new ConflictException("This bill is already marked as paid for that due date.");
        }

        var payment = new BillPayment { BillId = bill.Id, DueDate = dueDate, PaidOn = paidOn, Amount = bill.Amount, CreatedAt = clock.UtcNow };
        if (request.RecordExpense)
        {
            payment.Expense = new Expense
            {
                UserId = currentUser.Id,
                Amount = bill.Amount,
                Category = bill.Category,
                Description = Truncate($"{bill.Name} (bill due {dueDate:MMM d, yyyy})", 200),
                ExpenseDate = paidOn,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow,
            };
        }
        bill.Payments.Add(payment);

        try
        {
            await db.SaveChangesAsync(); // payment and its expense are saved together, or not at all
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE") == true)
        {
            throw new ConflictException("This bill is already marked as paid for that due date.");
        }
        return OccurrencesFor(bill, dueDate, dueDate, today).Single();
    }

    // Undo a payment. Its linked expense (if PennyWise created one) is removed too,
    // so undoing never leaves a stray expense behind.
    public async Task UnmarkPaidAsync(int billId, DateOnly dueDate)
    {
        var bill = await FindAsync(billId);
        var payment = bill.Payments.FirstOrDefault(p => p.DueDate == dueDate)
            ?? throw new NotFoundException("No payment is recorded for that due date.");
        if (payment.ExpenseId is { } expenseId)
        {
            var userId = currentUser.Id;
            var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId);
            if (expense is not null) db.Expenses.Remove(expense);
        }
        db.BillPayments.Remove(payment);
        await db.SaveChangesAsync();
    }

    private async Task<List<Bill>> LoadBillsAsync(bool includeArchived)
    {
        var userId = currentUser.Id;
        return await db.Bills.AsNoTracking().Include(b => b.Payments)
            .Where(b => b.UserId == userId && (includeArchived || b.ArchivedAt == null))
            .ToListAsync();
    }

    private async Task<Bill> FindAsync(int id)
    {
        var userId = currentUser.Id;
        return await db.Bills.Include(b => b.Payments).FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId)
            ?? throw new NotFoundException($"Bill {id} was not found.");
    }

    private static void Apply(Bill bill, BillRequest request, DateTime now)
    {
        bill.Name = request.Name!.Trim();
        bill.Amount = request.Amount!.Value;
        bill.Category = Enum.Parse<Category>(request.Category!);
        bill.Frequency = Enum.Parse<BillFrequency>(request.Frequency!);
        bill.FirstDueDate = request.FirstDueDate!.Value;
        bill.EndDate = request.EndDate;
        bill.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        bill.UpdatedAt = now;
    }

    // Due dates before the bill was added in PennyWise are not treated as owed;
    // a one-time bill is always owed on its date.
    public static DateOnly StartOfOwed(Bill bill) =>
        bill.Frequency == BillFrequency.OneTime ? bill.FirstDueDate : Max(bill.FirstDueDate, DateOnly.FromDateTime(bill.CreatedAt));

    public static IEnumerable<BillOccurrenceResponse> OccurrencesFor(Bill bill, DateOnly from, DateOnly to, DateOnly today)
    {
        foreach (var due in BillSchedule.Occurrences(bill, from, to))
        {
            var payment = bill.Payments.FirstOrDefault(p => p.DueDate == due);
            var days = due.DayNumber - today.DayNumber;
            var status = payment is not null ? "Paid"
                : days < 0 ? "Overdue"
                : days <= DueSoonDays ? "DueSoon"
                : "Upcoming";
            yield return new BillOccurrenceResponse(bill.Id, bill.Name, bill.Amount, bill.Category.ToString(),
                bill.Frequency.ToString(), due, status, days, payment?.PaidOn, payment?.Amount, payment?.ExpenseId);
        }
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
