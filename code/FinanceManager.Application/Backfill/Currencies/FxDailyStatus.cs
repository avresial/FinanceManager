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