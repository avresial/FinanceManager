namespace FinanceManager.Application.Services.Stocks;

public sealed record StockListing(
    string Symbol,
    string? Name,
    string? Exchange,
    string? AssetType,
    DateTime? IpoDate,
    DateTime? DelistingDate,
    string? Status);