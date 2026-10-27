using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Discovery;

public sealed record MarketDataSymbolResolution(
    MarketDataProvider Provider,
    int Priority,
    string Status,
    string? Symbol = null,
    string? ExchangeCode = null,
    string? Currency = null,
    string? Name = null,
    string? SecurityType = null,
    string? ExchangeName = null,
    string? Error = null);