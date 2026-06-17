using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceManager.Infrastructure.Services.Stocks;

/// <summary>
/// Decorates <see cref="OpenFigiClient"/> with an in-memory cache and a DB-backed lookup
/// against <see cref="IStockDetailsRepository"/>. Most dashboard tickers are already in
/// StockDetails, so this avoids hitting OpenFIGI on every price query.
/// </summary>
internal sealed class CachingIsinResolver(
    OpenFigiClient inner,
    IStockDetailsRepository stockDetailsRepository,
    IMemoryCache cache) : IIsinResolver
{
    private static readonly TimeSpan _positiveTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan _negativeTtl = TimeSpan.FromMinutes(5);

    // #Temporary — global OpenFIGI failure cooldown. When any inner call returns null
    // (HTTP error, rate-limit, timeout, or "not found"), skip OpenFIGI for 10 minutes
    // so the dashboard doesn't pay round-trip latency repeatedly during outages.
    // Remove once the OpenFIGI client distinguishes transport failures from empty results.
    private static readonly TimeSpan _failureCooldown = TimeSpan.FromMinutes(10);
    private static DateTime? _openFigiCooldownUntilUtc;

    public async Task<string?> ResolveAsync(string ticker, string? region = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;

        var normalized = ticker.Trim().ToUpperInvariant();
        var cacheKey = $"ISIN_RESOLVE_{normalized}_{region ?? string.Empty}";

        if (cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var fromDb = await stockDetailsRepository.GetByTicker(normalized, ct);
        // GetStoredTickers() on StockAccount actually returns ISINs post-#195; the upstream
        // call site forwards the entry's Isin in the "ticker" slot, so fall back to an ISIN
        // lookup before paying the OpenFIGI round-trip.
        if (fromDb is null)
            fromDb = await stockDetailsRepository.Get(normalized, ct);
        if (fromDb is not null && !string.IsNullOrWhiteSpace(fromDb.Isin))
        {
            cache.Set(cacheKey, fromDb.Isin, _positiveTtl);
            return fromDb.Isin;
        }

        // #Temporary — short-circuit OpenFIGI during a cooldown window after a prior failure.
        if (IsInOpenFigiCooldown())
            return null;

        var resolved = await inner.ResolveAsync(normalized, region, ct);
        if (resolved is null)
            _openFigiCooldownUntilUtc = DateTime.UtcNow.Add(_failureCooldown);

        cache.Set(cacheKey, resolved, resolved is null ? _negativeTtl : _positiveTtl);
        return resolved;
    }

    // #Temporary
    private static bool IsInOpenFigiCooldown()
    {
        var until = _openFigiCooldownUntilUtc;
        return until.HasValue && DateTime.UtcNow < until.Value;
    }
}