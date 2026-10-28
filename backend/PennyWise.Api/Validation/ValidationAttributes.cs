using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using PennyWise.Api.Models;

namespace PennyWise.Api.Validation;

// Reusable validation rules. [ApiController] runs them automatically and
// returns 400 Bad Request with the messages before any controller code runs.

// A peso amount: greater than 0, at most 10,000,000, and at most 2 decimal places.
public sealed class MoneyAttribute : ValidationAttribute
{
    public const decimal Max = 10_000_000m;

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is null) return ValidationResult.Success; // [Required] reports missing values.

        var amount = (decimal)value;
        var name = context.DisplayName;
        if (amount <= 0) return Fail($"{name} must be greater than 0.", context);
        if (amount > Max) return Fail($"{name} cannot exceed {Max:N0}.", context);
        if (decimal.Round(amount, 2) != amount) return Fail($"{name} can have at most 2 decimal places.", context);
        return ValidationResult.Success;
    }

    private static ValidationResult Fail(string message, ValidationContext context) =>
        new(message, [context.MemberName!]);
}

// One of the Category names, spelled exactly ("Food", not "food" or "0").
public sealed class CategoryNameAttribute : ValidationAttribute
{
    private static readonly string[] Names = Enum.GetNames<Category>();

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is null || Names.Contains((string)value)) return ValidationResult.Success;
        return new ValidationResult(
            $"Category must be one of: {string.Join(", ", Names)}.", [context.MemberName!]);
    }
}

// A month written as "YYYY-MM".
public sealed class MonthAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is null || MonthKey.IsValid((string)value)) return ValidationResult.Success;
        return new ValidationResult("Month must be in YYYY-MM format, e.g. 2026-10.", [context.MemberName!]);
    }
}

public static partial class MonthKey
{
    [GeneratedRegex(@"^(20\d{2})-(0[1-9]|1[0-2])$")]
    private static partial Regex Pattern();

    public static bool IsValid(string month) => Pattern().IsMatch(month);

    // "2026-10" -> 2026-10-01 and 2026-10-31
    public static (DateOnly First, DateOnly Last) Range(string month)
    {
        var first = DateOnly.ParseExact(month + "-01", "yyyy-MM-dd");
        return (first, first.AddMonths(1).AddDays(-1));
    }
}
