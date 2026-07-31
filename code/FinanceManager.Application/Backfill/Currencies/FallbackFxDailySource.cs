namespace FinanceManager.Application.Backfill.Currencies;

public sealed class FallbackFxDailySource(IReadOnlyList<IFxDailySource> sources) : IFxDailySource
{
    public string Name => "Fallback";
    public int Priority => 0;

    public async Task<FxDailyResult> GetDailyRatesAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        var rateLimited = false;
        var failed = false;
        foreach (var source in sources.OrderBy(x => x.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await source.GetDailyRatesAsync(fromCurrency, toCurrency, cancellationToken);
            if (result.Status == FxDailyStatus.Ok)
                return result;

            rateLimited |= result.Status == FxDailyStatus.RateLimited;
            failed |= result.Status == FxDailyStatus.Error;
        }

        return rateLimited ? FxDailyResult.RateLimited : failed ? FxDailyResult.Failed : FxDailyResult.Empty;
    }
}