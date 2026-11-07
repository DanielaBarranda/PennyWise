using System.ComponentModel.DataAnnotations;

namespace PennyWise.Api.DTOs;

// What can still be spent in one budget period (a day, a week or a month).
public record AllowanceResponse(
    string Period,            // Daily | Weekly | Monthly
    DateOnly Start,
    DateOnly End,
    string Status,            // Available | OverBudget | Ended | NoBudget
    decimal? Budget,
    decimal Spent,
    decimal Saved,            // moved into savings goals within the period (at least 0)
    decimal ReservedForBills, // unpaid bills due within the period
    decimal? Available,       // budget - spent - saved - reserved; negative when over budget
    int DaysLeft,             // in the period, today included
    decimal? Daily,           // what is left, divided by the days left
    decimal? Total);          // everything left in the period

public class PreferencesRequest
{
    [Required(ErrorMessage = "Choose a budget period.")]
    [AllowedValues("Daily", "Weekly", "Monthly", ErrorMessage = "Budget period must be Daily, Weekly or Monthly.")]
    public string? BudgetPeriod { get; set; }
}
