using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PennyWise.Api.Models;
using PennyWise.Api.Services;

namespace PennyWise.Api.Validation;

// Reads which budget period a request is about:
//   ?month=2026-10                      that month
//   ?period=Weekly&date=2026-11-04      the week (Monday-Sunday) containing that date
//   ?period=Daily&date=2026-11-04       that day
// A missing date means today (Manila time); a missing period means Monthly.
public static class PeriodQuery
{
    // Returns null, with the problem added to ModelState, when the query is invalid.
    public static BudgetWindow? Resolve(ModelStateDictionary modelState, string? month, string? period, string? date, DateOnly today)
    {
        var parsedPeriod = BudgetPeriod.Monthly;
        if (period is not null && !(Enum.TryParse(period, ignoreCase: false, out parsedPeriod) && Enum.IsDefined(parsedPeriod) && !int.TryParse(period, out _)))
        {
            modelState.AddModelError(nameof(period), "Period must be Daily, Weekly or Monthly.");
            return null;
        }
        if (month is not null && !MonthKey.IsValid(month))
        {
            modelState.AddModelError(nameof(month), "Month must be in YYYY-MM format, e.g. 2026-10.");
            return null;
        }

        DateOnly? parsedDate = null;
        if (date is not null)
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) || d.Year is < 2000 or > 2099)
            {
                modelState.AddModelError(nameof(date), "Date must be in YYYY-MM-DD format, e.g. 2026-11-04.");
                return null;
            }
            parsedDate = d;
        }

        var anchor = parsedDate ?? (month is not null ? MonthKey.Range(month).First : today);
        return BudgetWindow.For(parsedPeriod, anchor);
    }
}
