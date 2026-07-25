using BenchmarkDotNet.Running;
using FinanceManager.Benchmarks.Configuration;
using FinanceManager.Benchmarks.Validation;

namespace FinanceManager.Benchmarks;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Contains("--validate", StringComparer.OrdinalIgnoreCase))
                return await EndpointValidator.Run() ? 0 : 1;

            BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(WithDefaultFilter(args), BenchmarkConfig.Create());

            return 0;
        }
        finally
        {
            // Releases the API host and deletes this run's working copy of the database. With the
            // in-process toolchain the benchmarks ran here, so there is a real environment to tear down.
            Hosting.BenchmarkEnvironment.Shutdown();
        }
    }

    /// <summary>
    /// Runs the whole suite when no filter was given, instead of dropping into BenchmarkDotNet's
    /// interactive picker — a benchmark run started from CI or a background shell has nobody to answer it.
    /// </summary>
    private static string[] WithDefaultFilter(string[] args) =>
        args.Any(arg => arg.Equals("--filter", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("-f", StringComparison.Ordinal))
            ? args
            : [.. args, "--filter", "*"];
}