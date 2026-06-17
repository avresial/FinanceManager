using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Shared;

/// <summary>
/// Validates that a date used by a request model is within the supported financial data range.
/// </summary>
/// <remarks>
/// Apply to properties or parameters that should be on or after 1900-01-01 and no later than one
/// year from the current UTC date. Null values are valid; combine with <see cref="RequiredAttribute"/>
/// when a value must be supplied.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ReasonableDateAttribute : ValidationAttribute
{
    /// <summary>
    /// The earliest date accepted by the validator.
    /// </summary>
    private static readonly DateTime _minimumDate = new(1900, 1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="ReasonableDateAttribute"/> class.
    /// </summary>
    public ReasonableDateAttribute()
    {
        ErrorMessage = "Date must be on or after 1900-01-01 and no later than one year from today.";
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not DateTime date)
            return false;

        var maximumDate = DateTime.UtcNow.Date.AddYears(1);
        return date.Date >= _minimumDate && date.Date <= maximumDate;
    }
}