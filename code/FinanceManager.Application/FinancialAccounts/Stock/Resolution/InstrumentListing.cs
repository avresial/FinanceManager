namespace FinanceManager.Application.FinancialAccounts.Stock.Resolution;

/// <summary>
/// Reconciled candidate instrument listing after cross-referencing Alpha Vantage and OpenFIGI data.
/// Represents a single venue or entry for an instrument.
/// </summary>
/// <param name="ShareClassFigi">
/// Share-class FIGI from OpenFIGI — the canonical identity key. Auto-resolution requires this (plus
/// an Alpha Vantage symbol); ISIN is only an optional cross-reference.
/// </param>
/// <param name="CompositeFigi">Composite/country-level FIGI, stored alongside the share-class FIGI.</param>
/// <param name="QuoteFactor">
/// Multiplier that converts a quote in <see cref="Currency"/>'s provider quoting unit into
/// <see cref="Currency"/> (e.g. 0.01 for GBX → GBP). Defaults to <c>1</c>.
/// </param>
/// <param name="AssetListingId">
/// Id of the persisted <see cref="FinanceManager.Domain.Assets.Entities.AssetListing"/> when the
/// match was upserted into the new asset model, otherwise <c>null</c>. Lets callers attach a
/// transaction to a concrete listing rather than re-resolving by ticker.
/// </param>
/// <param name="QuoteCurrency">
/// Currency the market-data provider actually quotes this listing in (e.g. "GBX" pence), which can
/// differ from the canonical <see cref="Currency"/> (e.g. "GBP"). When <c>null</c> the provider
/// quotes in <see cref="Currency"/>. Drives <c>MarketDataSymbol.Currency</c> and the listing's
/// price multiplier so pence-quoted listings are normalised instead of defaulting to 1.
/// </param>
public sealed record InstrumentListing(
    string? Isin,
    string? AlphaVantageSymbol,
    string Name,
    string Exchange,
    string Currency,
    string Type,
    string? ShareClassFigi = null,
    string? CompositeFigi = null,
    decimal QuoteFactor = 1m,
    long? AssetListingId = null,
    string? QuoteCurrency = null);