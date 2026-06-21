namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Reconciled candidate instrument listing after cross-referencing Alpha Vantage and OpenFIGI data.
/// Represents a single venue or entry for an instrument.
/// </summary>
/// <param name="AssetListingId">
/// Id of the persisted <see cref="FinanceManager.Domain.Assets.Entities.AssetListing"/> when the
/// match was upserted into the new asset model, otherwise <c>null</c>. Lets callers attach a
/// transaction to a concrete listing rather than re-resolving by ticker.
/// </param>
public sealed record InstrumentListing(
    string? Isin,
    string? AlphaVantageSymbol,
    string Name,
    string Exchange,
    string Currency,
    string Type,
    decimal QuoteFactor = 1m,
    long? AssetListingId = null);