using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FinanceManager.Application.Diagnostics;

/// <summary>
/// Central OpenTelemetry instrumentation for FinanceManager's business operations.
/// <see cref="ActivitySourceName"/> and <see cref="MeterName"/> are registered with
/// OpenTelemetry in the API host, so the spans and metrics created here surface in the
/// Aspire dashboard alongside the built-in ASP.NET Core, HttpClient and database telemetry.
/// </summary>
public static class FinanceManagerTelemetry
{
    public const string ActivitySourceName = "FinanceManager";
    public const string MeterName = "FinanceManager";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    // A static Meter is the documented OpenTelemetry pattern; it lives for the process lifetime
    // so it is intentionally never disposed.
    private static readonly Meter _meter = new(MeterName);

    public static readonly Counter<long> CurrencyImportJobs = _meter.CreateCounter<long>(
        "financemanager.currency_import.jobs",
        unit: "{job}",
        description: "Number of currency import jobs processed, tagged by outcome.");

    public static readonly Histogram<double> CurrencyImportDuration = _meter.CreateHistogram<double>(
        "financemanager.currency_import.duration",
        unit: "ms",
        description: "Duration of currency import jobs in milliseconds, tagged by outcome.");

    public static readonly Counter<long> InsightsGenerationRuns = _meter.CreateCounter<long>(
        "financemanager.insights.runs",
        unit: "{run}",
        description: "Number of AI insight generation runs, tagged by outcome.");

    public static readonly Histogram<double> InsightsGenerationDuration = _meter.CreateHistogram<double>(
        "financemanager.insights.duration",
        unit: "ms",
        description: "Duration of AI insight generation runs in milliseconds, tagged by outcome.");

    public static readonly Counter<long> AiChatRequests = _meter.CreateCounter<long>(
        "financemanager.ai.chat_requests",
        unit: "{request}",
        description: "Number of AI chat provider attempts, tagged by provider and outcome.");
}