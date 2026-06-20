namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// Represents the provider-specific symbol used to fetch prices for an <see cref="AssetListing"/>.
/// Kept separate from the listing because providers use different symbol formats
/// (e.g. "CSPX.LON", "CSPX.L", "SXR8.DE") or expose internal instrument IDs.
/// </summary>
public class MarketDataSymbol
{
    /// <summary>Internal database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Listing this provider symbol belongs to.</summary>
    public long AssetListingId { get; set; }

    /// <summary>Listing this provider symbol belongs to.</summary>
    public AssetListing AssetListing { get; set; } = null!;

    /// <summary>Provider this symbol is valid for (AlphaVantage, TwelveData, YahooFinance, Stooq, ...).</summary>
    public MarketDataProvider Provider { get; set; }

    /// <summary>Provider-specific symbol, e.g. "CSPX.LON", "CSPX.L", "SXR8.DE".</summary>
    public string Symbol { get; set; } = null!;

    /// <summary>Provider-specific exchange code, e.g. Alpha Vantage "LON" vs another provider's "LSE".</summary>
    public string? ProviderExchangeCode { get; set; }

    /// <summary>Provider-specific internal instrument ID, if the API exposes a stable ID better than the symbol.</summary>
    public string? ProviderInstrumentId { get; set; }

    /// <summary>Currency returned by this provider for this symbol. Usually matches the listing's trading currency.</summary>
    public string? Currency { get; set; }

    /// <summary>Whether this symbol is preferred for this listing/provider.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Whether this mapping can currently be used. Disable when the provider no longer returns data.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Last time the app successfully confirmed that this symbol works.</summary>
    public DateTimeOffset? LastValidatedAt { get; set; }

    /// <summary>Last time the app successfully fetched a price using this mapping.</summary>
    public DateTimeOffset? LastSuccessfulPriceFetchAt { get; set; }

    /// <summary>Last error returned by the provider, for diagnostics.</summary>
    public string? LastError { get; set; }

    /// <summary>Timestamp when the record was created in this system.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the record was last updated in this system.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}