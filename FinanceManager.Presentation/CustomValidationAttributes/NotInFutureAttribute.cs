using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Presentation.CustomValidationAttributes
{
    internal class NotInFutureAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null)
                return new ValidationResult("Value is null, validation can not be proceeded!"); ;

            DateTime dateTime = (DateTime)value;

            if (DateTime.UtcNow.CompareTo(dateTime.ToUniversalTime()) >= 0)
                return ValidationResult.Success!;

            return new ValidationResult("Date must not be in the future!");
        }
    }
}
