using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Discovery;

public sealed class InstrumentImportPreviewDto
{
    public required InstrumentDiscoveryResultDto Instrument { get; init; }
    public bool AssetAlreadyExists { get; init; }
    public bool ListingAlreadyExists { get; init; }
    public bool MarketDataSymbolAlreadyExists { get; init; }
    public bool CanImport { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record ImportInstrumentCommand(InstrumentDiscoveryResultDto Instrument);

public sealed class ImportedInstrumentDto
{
    public long AssetId { get; init; }
    public long AssetListingId { get; init; }
    public long? MarketDataSymbolId { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public string ExchangeName { get; init; } = string.Empty;
    public string TradingCurrency { get; init; } = string.Empty;
    public bool CreatedAsset { get; init; }
    public bool CreatedListing { get; init; }
    public bool CreatedMarketDataSymbol { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}