namespace FinanceManager.Domain.Assets.Entities;

/// <summary>
/// The kind of identifier stored in an <see cref="AssetIdentifier"/>.
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
    Ticker,
    Other
}