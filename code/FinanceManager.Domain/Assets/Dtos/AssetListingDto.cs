namespace FinanceManager.Domain.Assets.Dtos;

/// <summary>
/// Data transfer object for an asset listing used by the admin management API and UI.
/// Flat (no entity back-references); doubles as both request and response shape.
/// </summary>
public class AssetListingDto
{
    public long Id { get; set; }
    public long AssetId { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string ExchangeMic { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string TradingCurrency { get; set; } = string.Empty;
    public string? ListingFigi { get; set; }
    public string? ExchangeInstrumentId { get; set; }
    public bool IsPrimaryListing { get; set; }
    public decimal PriceMultiplier { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public List<MarketDataSymbolDto> MarketDataSymbols { get; set; } = [];
}