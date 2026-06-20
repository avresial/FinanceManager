namespace FinanceManager.Domain.FinancialAccounts.Investments.Entities;

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