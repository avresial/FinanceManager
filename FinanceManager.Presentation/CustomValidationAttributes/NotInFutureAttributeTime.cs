using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Presentation.CustomValidationAttributes
{
    internal class NotInFutureAttributeTime : ValidationAttribute
    {
        private readonly DateTime date;

        public NotInFutureAttributeTime(DateTime? date)
        {
            this.date = date is null ? DateTime.Now.Date : date.Value.Date;
        }
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            TimeSpan timespan = (TimeSpan)value;
            DateTime dt = date.Add(timespan);
            DateTime dtUtc = dt.ToUniversalTime();
            TimeSpan tsUtc = dtUtc.TimeOfDay;

            var compareReusult = DateTime.UtcNow.CompareTo(dtUtc);

            if (compareReusult >= 0)
                return ValidationResult.Success!;

            return new ValidationResult("Date must not be in the future!");
        }
    }

}
