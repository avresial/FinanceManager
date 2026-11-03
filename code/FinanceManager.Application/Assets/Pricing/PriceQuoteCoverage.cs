using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Shared;

namespace FinanceManager.Application.Assets.Pricing;

/// <summary>Day-window helpers and the coverage rule that decides when stored quotes need a provider fetch.</summary>
internal static class PriceQuoteCoverage
{
    public const int FetchLookbackDays = 7;

    public static bool NeedsFetch(IReadOnlyList<PriceQuote> existing, DateTime start, DateTime end)
    {
        // A weekend-only window can never become covered — providers publish no Saturday or Sunday
        // close — so reporting it as missing would re-request it on every single call.
        if (!MarketCalendar.HasMarketDay(start, end)) return false;

        if (existing is null || existing.Count == 0) return true;

        var existingDates = existing.Select(x => x.PriceTime.Date).ToHashSet();

        // Boundary-coverage check using the 7-day lookback tolerance, measured from the first and
        // last *trading* day in the window: ensure data exists within [start, start+7d] and
        // [end-7d, end]. This allows for market holidays and weekends without triggering a full
        // refetch, while genuine gaps at boundaries do.
        var marketStart = MarketCalendar.NextMarketDay(start);
        var marketEnd = MarketCalendar.LastMarketDay(end);

        var startBoundary = marketStart.AddDays(FetchLookbackDays);
        var hasStartCoverage = existingDates.Any(d => d >= marketStart && d <= startBoundary);

        var endBoundary = marketEnd.AddDays(-FetchLookbackDays);
        var hasEndCoverage = existingDates.Any(d => d >= endBoundary && d <= marketEnd);

        return !hasStartCoverage || !hasEndCoverage;
    }

    public static DateTimeOffset DayStart(DateTime date) => new(date.Date, TimeSpan.Zero);

    public static DateTimeOffset DayEnd(DateTime date) => new DateTimeOffset(date.Date, TimeSpan.Zero).AddDays(1).AddTicks(-1);
}