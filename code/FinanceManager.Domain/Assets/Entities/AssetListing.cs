using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// Represents a concrete exchange listing of an <see cref="Asset"/>. A transaction points here,
/// because buying CSPX on the London Stock Exchange in USD is not the same listing as buying
/// SXR8 for the same ETF on Xetra in EUR.
/// </summary>
public class AssetListing
{
    /// <summary>Internal database identifier.</summary>
    public long Id { get; set; }

    /// <summary>Parent asset identifier.</summary>
    public long AssetId { get; set; }

    /// <summary>Parent asset.</summary>
    public Asset Asset { get; set; } = null!;

    /// <summary>Ticker used on this exchange, e.g. "CSPX", "SXR8", "AAPL".</summary>
    public string Ticker { get; set; } = null!;

    /// <summary>ISO 10383 Market Identifier Code, e.g. "XLON" (London Stock Exchange), "XETR" (Xetra), or <c>null</c> when the venue was not recognized during import.</summary>
    public string? ExchangeMic { get; set; }

    /// <summary>Human-readable exchange name, e.g. "London Stock Exchange".</summary>
    public string ExchangeName { get; set; } = string.Empty;

    /// <summary>Currency in which this listing is traded, e.g. "USD", "EUR", "GBX", or <c>null</c> when the trading currency was not provided during import.</summary>
    public string? TradingCurrency { get; set; }

    /// <summary>FIGI for this exact listing, if available. More specific than composite/share-class FIGI.</summary>
    public string? ListingFigi { get; set; }

    /// <summary>Local exchange-specific instrument identifier, if available.</summary>
    public string? ExchangeInstrumentId { get; set; }

    /// <summary>Whether this is the main/default listing for the asset (used when choosing a default price source).</summary>
    public bool IsPrimaryListing { get; set; }

    /// <summary>Price multiplier used to normalise quote values, e.g. 0.01 to convert GBX (pence) to GBP (pounds).</summary>
    public decimal PriceMultiplier { get; set; } = 1m;

    /// <summary>Whether this listing is currently active. Delisted listings are kept but marked inactive.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Timestamp when the record was created in this system.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp when the record was last updated in this system.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Provider-specific symbols for this listing.</summary>
    public ICollection<MarketDataSymbol> MarketDataSymbols { get; set; } = [];

    /// <summary>Whether at least one enabled provider symbol is configured for automatic price fetching.</summary>
    [NotMapped]
    public bool HasEnabledSymbol => MarketDataSymbols.Any(x => x.IsEnabled);

    /// <summary>
    /// Whether this listing is missing exchange/currency metadata, has an unclassified parent asset,
    /// or has no enabled provider symbol for automatic prices.
    /// </summary>
    [NotMapped]
    public bool IsIncomplete =>
        string.IsNullOrWhiteSpace(ExchangeMic)
        || string.IsNullOrWhiteSpace(TradingCurrency)
        || (Asset is not null && Asset.Type == AssetType.Other)
        || !HasEnabledSymbol;

    /// <summary>
    /// Returns <see cref="TradingCurrency"/> when known, falling back to <paramref name="transactionCurrency"/>
    /// when the listing's trading currency has not been curated yet.
    /// </summary>
    public string ResolveCurrencyOrFallback(string transactionCurrency) =>
        !string.IsNullOrWhiteSpace(TradingCurrency) ? TradingCurrency : transactionCurrency;
}