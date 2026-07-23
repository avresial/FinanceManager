namespace FinanceManager.Application.Backfill.Currencies;

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