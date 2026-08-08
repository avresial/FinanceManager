using FinanceManager.Application.Shared.Options;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Application.Backfill.Currencies;

/// <summary>
/// Startup currency exchange-rate backfill. For each discovered pair it skips already-fresh pairs,
/// calls the configured market-data fallback chain once for stale pairs, and inserts only the dates missing from
/// the database. Provider calls are sequential (free-tier rate limits); a quota response stops the
/// run early while every row inserted so far stays committed for the next startup to resume from.
/// </summary>
public sealed class CurrencyRateBackfillService(
    ICurrencyPairDiscoveryService pairDiscovery,
    IFxDailySource fxSource,
    IExchangeRateRepository exchangeRateRepository,
    IOptions<BackfillOptions> options,
    ILogger<CurrencyRateBackfillService> logger) : IBackfillService
{
    public string Name => "CurrencyRates";

    public int Order => 20;

    public async Task<BackfillResult> BackfillAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.CurrencyRatesEnabled)
        {
            logger.LogInformation("Currency-rate backfill is disabled by configuration; skipping.");
            return BackfillResult.Empty;
        }

        var lookback = Math.Max(1, options.Value.CurrencyLookbackDays);
        var nowUtc = DateTime.UtcNow;
        // FX markets are closed at weekends, so a Saturday/Sunday restart expects nothing newer
        // than Friday — treating Friday as "latest" keeps weekend restarts from re-requesting a
        // pair that is already up to date.
        var expectedLatest = MarketCalendar.LastMarketDay(nowUtc);
        var windowStart = nowUtc.Date.AddDays(-lookback);

        var pairs = await pairDiscovery.GetPairsAsync(cancellationToken);

        var inspected = 0;
        var requests = 0;
        var inserted = 0;
        var skipped = 0;
        var failures = 0;
        var rateLimited = false;

        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            inspected++;

            // Freshness gate: if the newest stored point is already the last closed market day we make
            // no call. This also stops weekend and same-UTC-day restarts from re-hitting the provider,
            // since the newest stored date won't advance until a new market day closes.
            var latestStored = await exchangeRateRepository.GetLatestDate(pair.From, pair.To, cancellationToken);
            if (latestStored is DateTime latest && latest.Date >= expectedLatest)
            {
                skipped++;
                continue;
            }

            requests++;
            var result = await fxSource.GetDailyRatesAsync(pair.From, pair.To, cancellationToken);

            if (result.Status == FxDailyStatus.RateLimited)
            {
                // Quota exhausted. Stop calling the provider for the rest of this run; already-inserted
                // rows remain committed and the next startup continues idempotently from here.
                rateLimited = true;
                logger.LogWarning(
                    "Currency backfill exhausted its provider quota at pair {From}->{To}; stopping this run. " +
                    "Inserted {Inserted} rows so far; remaining pairs resume on next start.",
                    pair.From, pair.To, inserted);
                break;
            }

            if (result.Status != FxDailyStatus.Ok || result.Points.Count == 0)
            {
                if (result.Status == FxDailyStatus.Error) failures++;
                continue;
            }

            // Only keep points inside the lookback window; older history isn't needed on startup.
            var windowPoints = result.Points
                .Where(p => p.Key.Date >= windowStart && p.Value > 0)
                .Select(p => (Date: p.Key, Rate: p.Value))
                .ToList();

            if (windowPoints.Count == 0)
                continue;

            var minDate = windowPoints.Min(p => p.Date);
            var maxDate = windowPoints.Max(p => p.Date);
            var existingDates = (await exchangeRateRepository.GetExistingDates(pair.From, pair.To, minDate, maxDate, cancellationToken))
                .Select(d => d.Date)
                .ToHashSet();

            var missing = windowPoints.Where(p => !existingDates.Contains(p.Date.Date)).ToList();
            skipped += windowPoints.Count - missing.Count;

            if (missing.Count == 0)
                continue;

            try
            {
                var written = await exchangeRateRepository.AddRange(pair.From, pair.To, missing, cancellationToken);
                inserted += written;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Currency backfill persistence cancelled for pair {From}->{To}.", pair.From, pair.To);
                throw;
            }
            catch (OperationCanceledException ex)
            {
                failures++;
                logger.LogDebug(ex, "Currency backfill persistence failed with a cancellation for pair {From}->{To}; continuing.", pair.From, pair.To);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                logger.LogError(ex, "Currency backfill failed to persist rates for pair {From}->{To}.", pair.From, pair.To);
            }
        }

        logger.LogInformation(
            "Currency-rate backfill finished: {Inspected} pairs inspected, {Requests} provider requests, " +
            "{Inserted} rows inserted, {Skipped} rows skipped, {Failures} failures, rate-limited: {RateLimited}.",
            inspected, requests, inserted, skipped, failures, rateLimited);

        return new BackfillResult(inspected, requests, inserted, skipped, failures, rateLimited);
    }
}