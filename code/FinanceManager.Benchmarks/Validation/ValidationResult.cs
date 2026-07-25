namespace FinanceManager.Benchmarks.Validation;

/// <summary>
/// Outcome of one validation call: how long it took, how much it returned, and why it failed if it did.
/// </summary>
/// <param name="Category">Benchmark class the call belongs to.</param>
/// <param name="Call">Human-readable description of the endpoint, taken from the benchmark attribute.</param>
/// <param name="Elapsed">Wall time for the single call. Indicative only — includes first-call warmup.</param>
/// <param name="PayloadBytes">Size of the response body; a zero or near-zero size on a list endpoint usually means the seeded data was missed.</param>
/// <param name="Error">Failure message, or null when the call succeeded.</param>
public sealed record ValidationResult(
    string Category,
    string Call,
    TimeSpan Elapsed,
    long PayloadBytes,
    string? Error);