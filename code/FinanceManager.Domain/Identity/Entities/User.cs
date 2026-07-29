using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Identity.Entities;

public class User
{
    public int UserId { get; set; }
    public required string Login { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public PricingLevel PricingLevel { get; set; } = PricingLevel.Free;
    public UserRole UserRole { get; set; } = UserRole.User;
    public required DateTime CreationDate { get; set; }

    /// <summary>Currency all asset values are presented in for this user. Defaults to PLN.</summary>
    public int PreferredCurrencyId { get; set; }

    /// <summary>Asset listing used as the investment benchmark; null selects Polish inflation.</summary>
    public long? PreferredBenchmarkListingId { get; set; }
}