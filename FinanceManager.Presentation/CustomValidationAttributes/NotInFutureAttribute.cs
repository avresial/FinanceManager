using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Presentation.CustomValidationAttributes
{
    internal class NotInFutureAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            value = (DateTime)value;
            if (DateTime.Now.CompareTo(value) >= 0)
            {
                return ValidationResult.Success;
            }
            else
            {
                return new ValidationResult("Date must not be in the future!");
            }
        }
    }
}
