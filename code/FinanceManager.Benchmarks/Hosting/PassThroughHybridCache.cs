using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Benchmarks.Hosting;

/// <summary>
/// A <see cref="HybridCache"/> that never stores anything: every read invokes the factory.
/// </summary>
/// <remarks>
/// BenchmarkDotNet invokes the same endpoint thousands of times, so a real cache is warm from the
/// second invocation onwards and the reported time collapses to "serialize an already-computed
/// result". That is a legitimate number — it is what a user clicking around a warm dashboard sees —
/// but it hides the repository and EF Core cost that a read-path refactor actually targets. Swapping
/// this in (<c>FM_BENCH_COLD_CACHE=1</c>) forces every request down to SQLite so both baselines are
/// measurable. See <see cref="Configuration.BenchmarkOptions.ColdCache"/>.
/// </remarks>
public sealed class PassThroughHybridCache : HybridCache
{
    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) => factory(state, cancellationToken);

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}