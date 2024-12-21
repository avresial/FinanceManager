using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Presentation.CustomValidationAttributes
{
    internal class NotInFutureAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            DateTime dateTime = (DateTime)value;

            if (DateTime.UtcNow.CompareTo(dateTime.ToUniversalTime()) >= 0)
                return ValidationResult.Success!;

            return new ValidationResult("Date must not be in the future!");
        }
    }
}
