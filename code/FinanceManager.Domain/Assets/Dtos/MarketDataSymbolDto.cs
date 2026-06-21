using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Dtos;

/// <summary>
/// Data transfer object for a provider-specific market data symbol used by the admin management API and UI.
/// Flat (no entity back-references); doubles as both request and response shape.
/// </summary>
public class MarketDataSymbolDto
{
    public long Id { get; set; }
    public long AssetListingId { get; set; }
    public MarketDataProvider Provider { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string? ProviderExchangeCode { get; set; }
    public string? ProviderInstrumentId { get; set; }
    public string? Currency { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsEnabled { get; set; } = true;
}