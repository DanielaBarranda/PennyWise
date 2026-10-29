using System.ComponentModel.DataAnnotations;

namespace PennyWise.Api.DTOs;

public record AllowanceResponse(
    string Month,
    string Period,      // Daily | Weekly | Monthly (the one `Amount` is for)
    string Status,      // Available | OverBudget | MonthEnded | NoBudget
    decimal? Budget,
    decimal Spent,
    decimal ReservedForBills, // unpaid bills still due this month
    decimal? Available,       // budget - spent - reserved; negative when over budget
    int DaysLeft,             // in the month, today included
    int DaysLeftInWeek,       // today to Sunday, within the month
    decimal? Amount,          // suggested allowance for the chosen period
    decimal? Daily,
    decimal? Weekly,
    decimal? Monthly);

public class PreferencesRequest
{
    [Required(ErrorMessage = "Choose an allowance view.")]
    [AllowedValues("Daily", "Weekly", "Monthly", ErrorMessage = "Allowance view must be Daily, Weekly or Monthly.")]
    public string? AllowancePeriod { get; set; }
}
