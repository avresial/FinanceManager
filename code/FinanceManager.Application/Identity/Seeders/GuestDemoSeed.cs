namespace FinanceManager.Application.Identity.Seeders;

/// <summary>
/// Deterministic randomness source for guest demo data. Every guest session seeded on the same UTC calendar date
/// gets the same data; the boundary is UTC midnight. Each session creates its own <see cref="Random"/> instances,
/// so concurrent sessions never share mutable state.
/// </summary>
public record GuestDemoSeed(DateOnly Date)
{
    public const int CashSalt = 1;
    public const int InvestmentPricesSalt = 2;

    /// <summary>Date encoded as yyyyMMdd, e.g. 20261014.</summary>
    public int Seed => Date.Year * 10000 + Date.Month * 100 + Date.Day;

    /// <summary>Creates an independent random stream; the salt keeps domains from influencing each other.</summary>
    public Random CreateRandom(int salt) => new(unchecked(Seed * 31 + salt));
}