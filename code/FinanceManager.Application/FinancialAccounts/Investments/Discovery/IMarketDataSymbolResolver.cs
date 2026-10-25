using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public interface IMarketDataSymbolResolver
{
    MarketDataProvider Provider { get; }
    int Priority { get; }
    Task<MarketDataSymbolResolution> ResolveAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default);
    Task<bool> ValidateAsync(MarketDataSymbolResolution candidate, CancellationToken ct = default);
}