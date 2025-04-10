using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Components.CustomValidationAttributes;

internal class NotInFutureAttributeTime : ValidationAttribute
{
    private readonly DateTime _date;

    public NotInFutureAttributeTime(DateTime? date)
    {
        this._date = date is null ? DateTime.Now.Date : date.Value.Date;
    }

    protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null) return new ValidationResult("Value is null, validation can not be proceeded!");

        TimeSpan timespan = (TimeSpan)value;
        DateTime dt = _date.Add(timespan);
        DateTime dtUtc = dt.ToUniversalTime();
        TimeSpan tsUtc = dtUtc.TimeOfDay;

        var compareReusult = DateTime.UtcNow.CompareTo(dtUtc);

        if (compareReusult >= 0) return ValidationResult.Success!;

        return new ValidationResult("Date must not be in the future!");
    }
}
