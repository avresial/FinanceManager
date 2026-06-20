namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// The kind of identifier stored in an <see cref="AssetIdentifier"/>. These are asset/share-class
/// level identifiers; venue-specific tickers live on <see cref="AssetListing"/> /
/// <see cref="MarketDataSymbol"/>, not here, because tickers are not globally unique.
/// </summary>
public enum AssetIdentifierType
{
    ISIN,
    FIGI,
    CompositeFIGI,
    ShareClassFIGI,
    CUSIP,
    SEDOL,
    WKN,
    Other
}