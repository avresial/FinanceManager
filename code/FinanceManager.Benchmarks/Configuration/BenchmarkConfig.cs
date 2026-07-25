using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Perfolizer.Horology;

namespace FinanceManager.Benchmarks.Configuration;

/// <summary>
/// BenchmarkDotNet configuration for the API suite.
/// </summary>
/// <remarks>
/// <para>
/// Runs in-process by default. The usual reason to prefer a child process — isolating the benchmarked
/// code from the harness's own JIT and GC state — matters most for nanosecond-scale microbenchmarks;
/// these endpoints are milliseconds of I/O and aggregation, where the isolation buys little. In
/// exchange, an in-process run needs no build at runtime, boots the API once for the whole suite, and
/// lets every benchmark class share one seeded database. Set <c>FM_BENCH_OUT_OF_PROCESS=1</c> to use
/// BenchmarkDotNet's default per-benchmark child processes instead and compare.
/// </para>
/// <para>
/// The memory diagnoser is on: allocation per request is the most actionable single number for a
/// read-path refactor, and it is far more stable across machines than wall time. It costs an extra run
/// per benchmark.
/// </para>
/// </remarks>
public static class BenchmarkConfig
{
    /// <summary>
    /// Builds the suite's configuration on top of <see cref="DefaultConfig.Instance"/>, which supplies the
    /// console logger, the standard columns, and the validators and analysers that flag a misconfigured run.
    /// </summary>
    public static IConfig Create()
    {
        var job = Job.Default
            .WithWarmupCount(3)
            .WithIterationCount(10)
            // Requests are milliseconds, not nanoseconds; the default 500 ms target would spend most of a
            // run on invocation counts that add no statistical value.
            .WithIterationTime(TimeInterval.FromMilliseconds(250));

        if (!UseOutOfProcessToolchain)
            job = job.WithToolchain(InProcessEmitToolchain.Instance);

        return ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(job)
            .AddDiagnoser(MemoryDiagnoser.Default)
            // P95 alongside the mean, because a smoother UX is mostly about the slow requests.
            .AddColumn(StatisticColumn.Median, StatisticColumn.P95)
            // Full JSON so a later run can be diffed against this one programmatically; the GitHub
            // markdown export that DefaultConfig already provides covers pasting results into a PR.
            .AddExporter(JsonExporter.Full)
            .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond))
            // One shared host and one shared database file mean the benchmark classes are not
            // independent; a joined summary reports them as the single suite they actually are.
            .WithOptions(ConfigOptions.JoinSummary);
    }

    private static bool UseOutOfProcessToolchain =>
        Environment.GetEnvironmentVariable("FM_BENCH_OUT_OF_PROCESS") is "1" or "true";
}