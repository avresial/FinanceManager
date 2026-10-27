using FinanceManager.Domain.Assets.Discovery;

namespace FinanceManager.Application.FinancialAccounts.Investments.Discovery;

public sealed class MarketDataSymbolResolutionService(IEnumerable<IMarketDataSymbolResolver> resolvers)
{
    public async Task<IReadOnlyList<MarketDataSymbolResolution>> ResolveAsync(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default)
    {
        var results = new List<MarketDataSymbolResolution>();
        foreach (var resolver in resolvers.OrderBy(x => x.Priority))
        {
            MarketDataSymbolResolution? candidate = null;
            try
            {
                ct.ThrowIfCancellationRequested();
                candidate = await resolver.ResolveAsync(instrument, ct);
                if (candidate.Status == "candidate")
                {
                    var valid = await resolver.ValidateAsync(candidate, ct);
                    candidate = candidate with
                    {
                        Status = valid ? "resolved" : "provider unavailable",
                        Error = valid ? null : "No positive recent price returned; retry validation later."
                    };
                }
                results.Add(candidate);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // Provider exceptions can contain credential-bearing request URLs. Expose only a safe diagnostic.
                results.Add((candidate ?? new(resolver.Provider, resolver.Priority, "provider unavailable")) with
                { Status = "provider unavailable", Error = "Provider request failed or timed out; retry later." });
            }
        }
        return results;
    }
}