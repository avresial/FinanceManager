using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Presentation.CustomValidationAttributes
{
    internal class NotInFutureAttributeTime : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            TimeSpan timespan = (TimeSpan)value;
            DateTime dt = DateTime.Now.Date.Add(timespan);
            DateTime dtUtc = dt.ToUniversalTime();
            TimeSpan tsUtc = dtUtc.TimeOfDay;

            var now = DateTime.UtcNow;
            var compareReusult = now.CompareTo(dtUtc);

            if (compareReusult >= 0)
            {
                return ValidationResult.Success!;
            }
            else
            {
                return new ValidationResult("Date must not be in the future!");
            }
        }
    }

}
