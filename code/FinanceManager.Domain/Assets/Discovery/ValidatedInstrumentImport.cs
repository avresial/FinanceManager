namespace FinanceManager.Domain.Assets.Discovery;

public sealed record ValidatedInstrumentImport(
    InstrumentDiscoveryResultDto Instrument,
    IReadOnlyList<MarketDataSymbolResolution> ProviderResults);