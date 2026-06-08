using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ReasonableDateAttribute : ValidationAttribute
{
    private static readonly DateTime _minimumDate = new(1900, 1, 1);

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