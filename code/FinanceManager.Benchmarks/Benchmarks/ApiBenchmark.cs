using BenchmarkDotNet.Attributes;
using FinanceManager.Benchmarks.Hosting;
using System.Globalization;
using System.Net.Http.Json;

namespace FinanceManager.Benchmarks.Benchmarks;

/// <summary>
/// Shared plumbing for every endpoint benchmark: acquires the shared host in <c>[GlobalSetup]</c> and
/// issues requests that consume the whole response body.
/// </summary>
public abstract class ApiBenchmark
{
    private BenchmarkEnvironment _api = default!;

    /// <summary>Ids and date window of the seeded dataset.</summary>
    protected BenchmarkScenario Scenario => _api.Scenario;

    /// <summary>The shared host, for benchmarks that need to reset state between iterations.</summary>
    protected BenchmarkEnvironment Api => _api;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        await Initialize();
        await Prime();
    }

    /// <summary>
    /// Acquires the shared host and applies <see cref="Configure"/>, but does not prime. Validation uses
    /// this so a failing route is reported as one failed row rather than aborting the whole report from
    /// inside <see cref="Prime"/>.
    /// </summary>
    public async Task Initialize()
    {
        _api = await BenchmarkEnvironment.GetOrCreate();
        Configure();
    }

    /// <summary>
    /// Derives whatever the benchmark methods need from <see cref="Scenario"/>. Runs before any request
    /// is issued, on both the benchmark and validation paths.
    /// </summary>
    protected virtual void Configure()
    {
    }

    /// <summary>
    /// Runs the benchmarked routes once before measurement starts. This pays the one-off costs that
    /// would otherwise land on whichever iteration happened to go first — EF Core model building and
    /// query-plan compilation, controller and JSON metadata, SQLite page cache warmup — none of which a
    /// user experiences per request.
    /// </summary>
    protected virtual Task Prime() => Task.CompletedTask;

    /// <summary>
    /// Issues a GET and reads the entire response body, returning its size so the result cannot be
    /// optimized away.
    /// </summary>
    /// <remarks>
    /// Reading to completion is what pulls JSON serialization into the measurement. Without it the
    /// benchmark would stop at "headers written" and a change to a response DTO's shape would look free.
    /// </remarks>
    protected async Task<long> Get(string route)
    {
        using var response = await _api.Client.GetAsync(route);
        var payload = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildFailureMessage("GET", route, (int)response.StatusCode, payload));

        return payload.LongLength;
    }

    /// <summary>Issues a POST with a JSON body and reads the entire response body.</summary>
    protected async Task<long> Post<TBody>(string route, TBody body)
    {
        using var response = await _api.Client.PostAsJsonAsync(route, body);
        var payload = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildFailureMessage("POST", route, (int)response.StatusCode, payload));

        return payload.LongLength;
    }

    /// <summary>Round-trip ("O") format, which is what the Blazor HTTP clients put on the wire.</summary>
    protected static string Iso(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>Round-trip format, escaped for use as a query-string value.</summary>
    protected static string IsoQuery(DateTime value) => Uri.EscapeDataString(Iso(value));

    // A non-success status means the benchmark is timing an error path, not the endpoint — surface the
    // body so the cause (403 from a stale id, 400 from a rejected date) is obvious rather than guessed.
    private static string BuildFailureMessage(string method, string route, int statusCode, byte[] payload)
    {
        var body = payload.Length == 0 ? "<empty>" : System.Text.Encoding.UTF8.GetString(payload);
        return $"{method} {route} returned {statusCode}. The benchmark would be measuring a failure path. Response: {body}";
    }
}