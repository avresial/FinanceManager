namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

/// <summary>
/// Tuning knobs for the range/bucket cache layer in <see cref="CachedAccountEntryRepository{T}"/>.
/// Eligibility (horizon) and TTL are independent knobs — the horizon governs which months may be
/// cached; the TTLs govern how long a cached bucket lives. See issue #456.
/// </summary>
public sealed class EntryRangeCacheOptions
{
    /// <summary>
    /// How many calendar months back from today are eligible for bucket caching. Requests for data
    /// older than this horizon are passed through uncached. Default: 12.
    /// </summary>
    public int HorizonMonths { get; init; } = 12;

    /// <summary>
    /// TTL for the current (open) month's bucket. Short to cap worst-case staleness from any write
    /// path that bypasses per-user tag invalidation. Default: 60 s.
    /// </summary>
    public TimeSpan OpenMonthTtl { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// TTL for fully elapsed month buckets. Long because only per-user tag invalidation can change
    /// their content — any write already busts the tag. Default: 1 h.
    /// </summary>
    public TimeSpan ElapsedMonthTtl { get; init; } = TimeSpan.FromHours(1);
}