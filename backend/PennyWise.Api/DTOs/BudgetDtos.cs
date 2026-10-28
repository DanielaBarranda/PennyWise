using System.ComponentModel.DataAnnotations;
using PennyWise.Api.Models;
using PennyWise.Api.Validation;

namespace PennyWise.Api.DTOs;

public class BudgetRequest : IValidatableObject
{
    [Required(ErrorMessage = "Month is required.")]
    [Month]
    public string? Month { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Money]
    public decimal? Amount { get; set; }

    public List<CategoryLimitRequest> CategoryLimits { get; set; } = [];

    // Runs after the attribute checks above have passed.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var duplicates = CategoryLimits.GroupBy(l => l.Category).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var category in duplicates)
        {
            yield return new ValidationResult(
                $"{category} has more than one limit.", [nameof(CategoryLimits)]);
        }

        if (CategoryLimits.Sum(l => l.Amount ?? 0) > Amount)
        {
            yield return new ValidationResult(
                "Category limits add up to more than the monthly budget.", [nameof(CategoryLimits)]);
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

public record BudgetResponse(int Id, string Month, decimal Amount, List<CategoryLimitResponse> CategoryLimits)
{
    public static BudgetResponse From(Budget b) =>
        new(b.Id, b.Month, b.Amount,
            b.CategoryLimits
                .OrderBy(l => l.Category)
                .Select(l => new CategoryLimitResponse(l.Category.ToString(), l.Amount))
                .ToList());
}
