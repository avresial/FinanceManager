namespace FinanceManager.Domain.Assets.Discovery;

public sealed record InvestmentInstrumentOptionDto
{
    public string ResultId { get; init; } = string.Empty;
    public InstrumentOptionSource Source { get; init; }
    public long? AssetListingId { get; init; }
    public InstrumentDiscoveryResultDto? ExternalInstrument { get; init; }
    public string Ticker { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? ExchangeMic { get; init; }
    public string ExchangeName { get; init; } = string.Empty;
    public string TradingCurrency { get; init; } = string.Empty;
    public string? Isin { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}