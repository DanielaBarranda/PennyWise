using System.ComponentModel.DataAnnotations;
using System.Globalization;
using PennyWise.Api.Models;
using PennyWise.Api.Services;
using PennyWise.Api.Validation;

namespace PennyWise.Api.DTOs;

// A monthly budget is identified by `month` (or a `start` on the 1st); weekly and
// daily budgets by `start` (a Monday for a week). Period defaults to Monthly.
public class BudgetRequest : IValidatableObject
{
    [AllowedValues("Daily", "Weekly", "Monthly", null, ErrorMessage = "Period must be Daily, Weekly or Monthly.")]
    public string? Period { get; set; }

    [Month]
    public string? Month { get; set; }

    [ValidDate]
    public DateOnly? Start { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Money]
    public decimal? Amount { get; set; }

    public List<CategoryLimitRequest> CategoryLimits { get; set; } = [];

    public BudgetPeriod ParsedPeriod => Period is null ? BudgetPeriod.Monthly : Enum.Parse<BudgetPeriod>(Period);

    // The period this budget covers. Only call after validation has passed.
    public BudgetWindow ToWindow() =>
        BudgetWindow.For(ParsedPeriod, Start ?? DateOnly.ParseExact(Month! + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture));

    // Runs after the attribute checks above have passed.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var period = ParsedPeriod;
        if (period == BudgetPeriod.Monthly && Start is null && Month is null)
        {
            yield return new ValidationResult("Month is required.", [nameof(Month)]);
        }
        else if (period != BudgetPeriod.Monthly && Start is null)
        {
            yield return new ValidationResult(period == BudgetPeriod.Weekly ? "Choose the week." : "Choose the day.", [nameof(Start)]);
        }
        else if (Start is { } start && !BudgetWindow.IsStart(period, start))
        {
            yield return new ValidationResult(
                period == BudgetPeriod.Weekly ? "A weekly budget starts on a Monday." : "A monthly budget starts on the 1st.", [nameof(Start)]);
        }

        var duplicates = CategoryLimits.GroupBy(l => l.Category).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var category in duplicates)
        {
            yield return new ValidationResult(
                $"{category} has more than one limit.", [nameof(CategoryLimits)]);
        }

        if (CategoryLimits.Sum(l => l.Amount ?? 0) > Amount)
        {
            yield return new ValidationResult(
                "Category limits add up to more than the budget.", [nameof(CategoryLimits)]);
        }
    }
}

public class CategoryLimitRequest
{
    [Required(ErrorMessage = "Category is required.")]
    [CategoryName]
    public string? Category { get; set; }

    [Required(ErrorMessage = "Limit amount is required.")]
    [Money]
    public decimal? Amount { get; set; }
}

public record CategoryLimitResponse(string Category, decimal Amount);

public record BudgetResponse(int Id, string Period, DateOnly Start, DateOnly End, string Month, decimal Amount, List<CategoryLimitResponse> CategoryLimits)
{
    public static BudgetResponse From(Budget b) =>
        new(b.Id, b.Period.ToString(), b.StartDate, BudgetWindow.For(b.Period, b.StartDate).End, b.Month, b.Amount,
            b.CategoryLimits
                .OrderBy(l => l.Category)
                .Select(l => new CategoryLimitResponse(l.Category.ToString(), l.Amount))
                .ToList());
}
