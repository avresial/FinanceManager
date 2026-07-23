namespace FinanceManager.Application.Backfill.Currencies;

/// <summary>
/// Fetches Alpha Vantage <c>FX_DAILY</c> series for a single currency pair. Separate from the
/// generic exchange-rate providers because the startup backfill needs the whole date-keyed series
/// in one call plus an explicit rate-limit signal, neither of which the per-date provider surface
/// exposes.
/// </summary>
public interface IFxDailySource
{
    /// <summary>
    /// One <c>FX_DAILY</c> request for <paramref name="fromCurrency"/> → <paramref name="toCurrency"/>.
    /// Never throws for provider-side failures; those come back as a non-<see cref="FxDailyStatus.Ok"/>
    /// status so the caller can decide whether to continue.
    /// </summary>
    Task<FxDailyResult> GetDailyRatesAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
}