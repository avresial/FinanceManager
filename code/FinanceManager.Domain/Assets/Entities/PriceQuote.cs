namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// Represents a cached price returned by a market data provider for an <see cref="AssetListing"/>.
/// Avoids re-fetching the same price repeatedly and records which provider returned what.
/// </summary>
public class PriceQuote
{
    /// <summary>Internal database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Listing for which this price was fetched.</summary>
    public long AssetListingId { get; set; }

    /// <summary>Listing for which this price was fetched.</summary>
    public AssetListing AssetListing { get; set; } = null!;

    /// <summary>Provider symbol mapping used to fetch this quote, if known.</summary>
    public long? MarketDataSymbolId { get; set; }

    /// <summary>Provider symbol mapping used to fetch this quote, if known.</summary>
    public MarketDataSymbol? MarketDataSymbol { get; set; }

    /// <summary>Market data provider that returned this quote.</summary>
    public MarketDataProvider Provider { get; set; }

    /// <summary>Price value as returned or normalised from the provider (after <see cref="AssetListing.PriceMultiplier"/>).</summary>
    public decimal Price { get; set; }

    /// <summary>Currency of the (normalised) price.</summary>
    public string Currency { get; set; } = null!;

    /// <summary>Date/time the price applies to. For daily prices this is typically the market close date.</summary>
    public DateTimeOffset PriceTime { get; set; }

    /// <summary>Whether this quote is real-time, delayed, end-of-day, or manual.</summary>
    public PriceQuoteType QuoteType { get; set; }

    /// <summary>Raw price before applying <see cref="AssetListing.PriceMultiplier"/>, if relevant.</summary>
    public decimal? RawPrice { get; set; }

    /// <summary>Raw currency returned by the provider, if it differs from the normalised currency.</summary>
    public string? RawCurrency { get; set; }

    /// <summary>Timestamp when the app fetched this quote.</summary>
    public DateTimeOffset FetchedAt { get; set; }
}