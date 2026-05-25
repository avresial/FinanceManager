using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Components.CustomValidationAttributes;

internal class NotInFutureAttributeTime : ValidationAttribute
{
    private readonly DateTime _date;

    public NotInFutureAttributeTime(DateTime? date) => this._date = date is null ? DateTime.Now.Date : date.Value.Date;

    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return new("Value is null, validation can not be proceeded!");
        if (value is not TimeSpan timespan) return new("Value is not a valid TimeSpan!");

        DateTime dt = _date.Add(timespan);

        if (DateTime.UtcNow.CompareTo(dt.ToUniversalTime()) >= 0) return ValidationResult.Success!;

        return new("Date must not be in the future!");
    }
}