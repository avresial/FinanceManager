using BenchmarkDotNet.Attributes;
using FinanceManager.Benchmarks.Benchmarks;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace FinanceManager.Benchmarks.Validation;

/// <summary>
/// Runs every benchmarked call exactly once and reports what came back.
/// </summary>
/// <remarks>
/// A benchmark that quietly measures a 403 or an empty result still produces a tidy table of numbers,
/// which is the worst possible failure mode for a baseline. This is the cheap guard: seconds instead of
/// minutes, it exercises the same methods the suite does (discovered by reflection, so it cannot drift
/// out of sync), and it prints the response size next to the timing — a suspiciously small payload is
/// usually an endpoint returning nothing because an id or date range missed the seeded data.
/// </remarks>
public static class EndpointValidator
{
    /// <summary>Returns true when every benchmarked call succeeded.</summary>
    public static async Task<bool> Run()
    {
        var results = new List<ValidationResult>();

        foreach (var type in DiscoverBenchmarkTypes())
        {
            var instance = (ApiBenchmark)Activator.CreateInstance(type)!;
            await instance.Initialize();

            foreach (var method in BenchmarkMethods(type))
                results.Add(await Invoke(instance, method));

            RunIterationCleanup(instance, type);
        }

        Report(results);
        return results.All(result => result.Error is null);
    }

    private static IEnumerable<Type> DiscoverBenchmarkTypes() =>
        typeof(EndpointValidator).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ApiBenchmark).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal);

    private static IEnumerable<MethodInfo> BenchmarkMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<BenchmarkAttribute>() is not null);

    private static async Task<ValidationResult> Invoke(ApiBenchmark instance, MethodInfo method)
    {
        var label = method.GetCustomAttribute<BenchmarkAttribute>()?.Description ?? method.Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var payloadBytes = await (Task<long>)method.Invoke(instance, null)!;
            stopwatch.Stop();
            return new ValidationResult(instance.GetType().Name, label, stopwatch.Elapsed, payloadBytes, Error: null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Reflection wraps whatever the endpoint threw; the inner exception carries the status code
            // and response body that actually explain the failure.
            var cause = (ex as TargetInvocationException)?.InnerException ?? ex;
            return new ValidationResult(instance.GetType().Name, label, stopwatch.Elapsed, PayloadBytes: 0, cause.Message);
        }
    }

    private static void RunIterationCleanup(ApiBenchmark instance, Type type)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.GetCustomAttribute<IterationCleanupAttribute>() is not null))
        {
            method.Invoke(instance, null);
        }
    }

    private static void Report(List<ValidationResult> results)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Category",-30} {"Call",-58} {"Time",10} {"Bytes",10}  Status");
        Console.WriteLine(new string('-', 125));

        foreach (var result in results)
        {
            var elapsed = result.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms";
            var status = result.Error is null ? "ok" : "FAILED: " + result.Error;
            Console.WriteLine($"{result.Category,-30} {Truncate(result.Call, 58),-58} {elapsed,10} {result.PayloadBytes,10}  {status}");
        }

        var failures = results.Count(result => result.Error is not null);
        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? $"All {results.Count} benchmarked calls succeeded."
            : $"{failures} of {results.Count} benchmarked calls failed.");
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";
}