namespace FinanceManager.Domain.Assets.Dtos;

public sealed record IncompleteAssetItem(
    long AssetId, string Name, long? ListingId, string? Ticker, string? ExchangeMic,
    string? TradingCurrency, int Holders, string? LastError,
    bool MissingVenue, bool MissingCurrency, bool MissingType, bool MissingIdentity,
    bool NoEnabledSymbol, bool AllSymbolsFailing, bool StalePrice)
{
    public IReadOnlyList<string> Reasons => new[]
    {
        ListingId is null ? "No listing" : null,
        MissingVenue ? "Missing venue" : null,
        MissingCurrency ? "Missing trading currency" : null,
        MissingType ? "Unclassified asset type" : null,
        MissingIdentity ? "Missing ISIN / FIGI" : null,
        NoEnabledSymbol ? "No enabled symbol" : null,
        AllSymbolsFailing ? "All enabled symbols failing" : null,
        StalePrice ? "No successful price in 7 days" : null
    }.OfType<string>().ToList();
}