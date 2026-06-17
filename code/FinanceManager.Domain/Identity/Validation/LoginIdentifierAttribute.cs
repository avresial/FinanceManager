using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Domain.Identity.Validation;

public sealed class LoginIdentifierAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute _emailAddress = new();
    private const string _guestLogin = "guest";

    public LoginIdentifierAttribute()
    {
        ErrorMessage = "Login must be a valid email address or guest.";
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not string login)
            return false;

        return string.Equals(login, _guestLogin, StringComparison.OrdinalIgnoreCase)
            || _emailAddress.IsValid(login);
    }
}