using PennyWise.Api.Models;

namespace PennyWise.Api.Services;

// Works out a bill's due dates from its first due date and frequency. Nothing is
// stored per occurrence, so the same due date can never appear twice.
public static class BillSchedule
{
    // Safety cap: a weekly bill over 20 years is ~1,040 occurrences.
    private const int MaxOccurrences = 2000;

    // Due dates between `from` and `to` (inclusive), in order.
    // Monthly and yearly dates are counted from the first due date each time, so
    // a bill due on the 31st falls on Feb 28/29 and then returns to the 31st.
    public static IEnumerable<DateOnly> Occurrences(Bill bill, DateOnly from, DateOnly to)
    {
        var last = bill.EndDate is { } end && end < to ? end : to;
        for (var n = 0; n < MaxOccurrences; n++)
        {
            var due = bill.Frequency switch
            {
                BillFrequency.OneTime => n == 0 ? bill.FirstDueDate : DateOnly.MaxValue,
                BillFrequency.Weekly => bill.FirstDueDate.AddDays(7 * n),
                BillFrequency.Monthly => bill.FirstDueDate.AddMonths(n),
                BillFrequency.Yearly => bill.FirstDueDate.AddYears(n),
                _ => DateOnly.MaxValue,
            };
            if (due > last) yield break;
            if (due >= from) yield return due;
        }
    }

    public static bool IsOccurrence(Bill bill, DateOnly date) =>
        Occurrences(bill, date, date).Any();
}
