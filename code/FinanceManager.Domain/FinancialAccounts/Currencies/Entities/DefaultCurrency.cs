namespace FinanceManager.Domain.FinancialAccounts.Currencies.Entities;

public static class DefaultCurrency
{
    public static Currency PLN => new(0, "PLN", "zł");
    public static Currency USD => new(1, "USD", "$");

    /// <summary>
    /// Codes and symbols seeded into the predefined currency list (besides <see cref="PLN"/> and
    /// <see cref="USD"/>). Ids are assigned at insert time so they never clash with currencies
    /// already added by imports.
    /// </summary>
    public static IReadOnlyList<(string ShortName, string Symbol)> CommonCurrencies =>
    [
        ("EUR", "€"),
        ("GBP", "£"),
        ("CHF", "CHF"),
        ("JPY", "¥"),
        ("CZK", "Kč"),
        ("SEK", "kr"),
        ("NOK", "kr"),
        ("DKK", "kr"),
        ("HUF", "Ft"),
        ("CAD", "C$"),
        ("AUD", "A$"),
    ];

    /// <summary>
    /// Minor "pence"-style quote units that legitimately appear as listing trading currencies and
    /// collapse to their major currency once a price multiplier is applied (1 GBX = 0.01 GBP).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> MinorQuoteUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["GBX"] = "GBP",
        ["GBP."] = "GBP",
        ["ZAC"] = "ZAR",
        ["ILA"] = "ILS",
    };
}