namespace FinanceManager.Application.Backfill.Currencies;

/// <summary>How an <see cref="IFxDailySource"/> request ended.</summary>
public enum FxDailyStatus
{
    /// <summary>Points were returned (see <see cref="FxDailyResult.Points"/>).</summary>
    Ok,

    /// <summary>The provider responded but had no data for the pair (unknown pair, unconfigured key).</summary>
    Empty,

    /// <summary>The provider's daily quota / rate limit is exhausted; stop making further calls this run.</summary>
    RateLimited,

    /// <summary>An unexpected error occurred for this pair; other pairs may still be attempted.</summary>
    Error
}

/// <summary>
/// Outcome of one <see cref="IFxDailySource.GetDailyRatesAsync"/> call.
/// <see cref="Points"/> is keyed by UTC date (day start) to the closing rate.
/// </summary>
/// <param name="Status">Terminal status of the request.</param>
/// <param name="Points">Returned daily closing rates; empty unless <see cref="Status"/> is <see cref="FxDailyStatus.Ok"/>.</param>
public readonly record struct FxDailyResult(FxDailyStatus Status, IReadOnlyDictionary<DateTime, decimal> Points)
{
    public static FxDailyResult Empty { get; } = new(FxDailyStatus.Empty, new Dictionary<DateTime, decimal>());
    public static FxDailyResult RateLimited { get; } = new(FxDailyStatus.RateLimited, new Dictionary<DateTime, decimal>());
    public static FxDailyResult Failed { get; } = new(FxDailyStatus.Error, new Dictionary<DateTime, decimal>());
    public static FxDailyResult Success(IReadOnlyDictionary<DateTime, decimal> points) => new(FxDailyStatus.Ok, points);
}

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