﻿using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Presentation.CustomValidationAttributes
{
    internal class NotInFutureAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            DateTime dateTime = (DateTime)value;
            var now = DateTime.UtcNow;
            var testResult = now.CompareTo(dateTime.ToUniversalTime());
            if (now.CompareTo(dateTime.ToUniversalTime()) >= 0)
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
