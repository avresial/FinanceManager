using Microsoft.Extensions.Logging;

namespace FinanceManager.Benchmarks.Configuration;

/// <summary>
/// Knobs for the benchmark harness, all supplied through environment variables so a run can be
/// re-shaped from CI or a shell without touching code. Every value has a working default.
/// </summary>
public static class BenchmarkOptions
{
    /// <summary>User id the seeded demo dataset belongs to, and the identity every request is signed for.</summary>
    public const int UserId = 424_242;

    /// <summary>Login name stamped into the benchmark JWT.</summary>
    public const string UserName = "benchmark";

    /// <summary>
    /// Deterministic, non-secret signing key. HMAC-SHA512 needs &gt;= 64 bytes or the API's startup
    /// guard rejects it.
    /// </summary>
    public const string JwtSigningKey = "benchmark-only-insecure-jwt-signing-key-do-not-use-anywhere-ever";

    /// <summary>
    /// Directory holding the seed template and the per-run working copy. Defaults to a
    /// <c>benchmark-data</c> folder next to the benchmark binaries so repeat runs reuse the template.
    /// Override with <c>FM_BENCH_DATA_DIR</c>.
    /// </summary>
    public static string DataDirectory =>
        Environment.GetEnvironmentVariable("FM_BENCH_DATA_DIR") is { Length: > 0 } dir
            ? Path.GetFullPath(dir)
            : Path.Combine(AppContext.BaseDirectory, "benchmark-data");

    /// <summary>
    /// The pristine seeded database. Built on first use, then reused verbatim so a baseline run and a
    /// post-refactor run measure byte-identical data.
    /// </summary>
    public static string TemplateDatabasePath => Path.Combine(DataDirectory, "seed-template.sqlite");

    /// <summary>
    /// Set <c>FM_BENCH_RESEED=1</c> to discard the template and generate a fresh dataset. Do this when
    /// the schema or the seeders change — otherwise leave it alone, because reseeding changes the data
    /// the numbers describe.
    /// </summary>
    public static bool ForceReseed => IsEnabled("FM_BENCH_RESEED");

    /// <summary>
    /// Set <c>FM_BENCH_COLD_CACHE=1</c> to swap <c>HybridCache</c> for a pass-through implementation.
    /// The default (warm) measures what a returning user experiences; cold measures the repository and
    /// EF Core work the cache normally hides, which is where a read-path refactor has headroom.
    /// </summary>
    public static bool ColdCache => IsEnabled("FM_BENCH_COLD_CACHE");

    /// <summary>
    /// Minimum log level for the benchmarked host. Warning by default so logging is not a measured cost
    /// while real failures still reach the console; set <c>FM_BENCH_LOG_LEVEL=Information</c> (or Debug)
    /// when diagnosing a failing endpoint.
    /// </summary>
    public static LogLevel LogLevel =>
        Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("FM_BENCH_LOG_LEVEL"), ignoreCase: true, out var level)
            ? level
            : Microsoft.Extensions.Logging.LogLevel.Warning;

    /// <summary>Months of demo history to seed. Six matches the in-app guest sandbox.</summary>
    public static int SeedMonths =>
        int.TryParse(Environment.GetEnvironmentVariable("FM_BENCH_SEED_MONTHS"), out var months) && months > 0
            ? months
            : 6;

    private static bool IsEnabled(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
        && (value.Equals("1", StringComparison.Ordinal) || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}