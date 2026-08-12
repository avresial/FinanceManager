using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Polly;
using Polly.Timeout;

namespace ServiceDefaults;
// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string _healthEndpointPath = "/health";
    private const string _alivenessEndpointPath = "/alive";
    private const string _healthDetailEndpointPath = "/health/detail";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddTransient<ExternalDependencyLoggingHandler>();
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            var attemptTimeoutSeconds = builder.Configuration.GetValue<int?>("HttpClientResilience:AttemptTimeoutSeconds") ?? 600;
            var totalRequestTimeoutSeconds = builder.Configuration.GetValue<int?>("HttpClientResilience:TotalRequestTimeoutSeconds") ?? 900;
            var circuitBreakerSamplingSeconds = builder.Configuration.GetValue<int?>("HttpClientResilience:CircuitBreakerSamplingDurationSeconds") ?? 1200;

            attemptTimeoutSeconds = Math.Max(1, attemptTimeoutSeconds);
            totalRequestTimeoutSeconds = Math.Max(attemptTimeoutSeconds, totalRequestTimeoutSeconds);
            circuitBreakerSamplingSeconds = Math.Max(attemptTimeoutSeconds * 2, circuitBreakerSamplingSeconds);

            // Turn on resilience by default
            http.AddHttpMessageHandler<ExternalDependencyLoggingHandler>()
                .AddStandardResilienceHandler(options =>
                {
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(attemptTimeoutSeconds);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(totalRequestTimeoutSeconds);
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(circuitBreakerSamplingSeconds);
                })
                .Configure((options, services) =>
                {
                    var logger = services.GetRequiredService<ILogger<ExternalDependencyLoggingHandler>>();
                    options.Retry.OnRetry = args =>
                    {
                        var request = args.Context.GetRequestMessage();
                        var result = args.Outcome.Result is { } response
                            ? ((int)response.StatusCode).ToString()
                            : args.Outcome.Exception?.GetType().Name ?? "unknown";

                        logger.LogWarning(
                            "External dependency retry. Service: {Service}; Host: {Host}; Method: {Method}; Path: {Path}; Result: {Result}; Attempt: {Attempt}; Execution Time: {ExecutionTimeMs}ms; Retry Delay: {RetryDelayMs}ms.",
                            ExternalDependencyLogging.GetService(request),
                            ExternalDependencyLogging.GetHost(request),
                            ExternalDependencyLogging.GetMethod(request),
                            ExternalDependencyLogging.GetPath(request),
                            result,
                            args.AttemptNumber,
                            args.Duration.TotalMilliseconds,
                            args.RetryDelay.TotalMilliseconds);

                        return ValueTask.CompletedTask;
                    };
                    options.AttemptTimeout.OnTimeout = args =>
                        LogTimeout(logger, args, "attempt");
                    options.TotalRequestTimeout.OnTimeout = args =>
                        LogTimeout(logger, args, "total request");
                });

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    private static ValueTask LogTimeout(
        ILogger logger,
        OnTimeoutArguments args,
        string timeoutType)
    {
        var request = args.Context.GetRequestMessage();

        logger.LogWarning(
            "External dependency timeout. Service: {Service}; Host: {Host}; Method: {Method}; Path: {Path}; Timeout Type: {TimeoutType}; Timeout: {TimeoutMs}ms.",
            ExternalDependencyLogging.GetService(request),
            ExternalDependencyLogging.GetHost(request),
            ExternalDependencyLogging.GetMethod(request),
            ExternalDependencyLogging.GetPath(request),
            timeoutType,
            args.Timeout.TotalMilliseconds);

        return ValueTask.CompletedTask;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    // Npgsql emits a span per database command on the "Npgsql" ActivitySource;
                    // capturing it surfaces every SQL query as a child span in the Aspire dashboard.
                    .AddSource("Npgsql")
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(_healthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(_alivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // PRODUCTION NOTE: no exporter is wired up unless the OTEL_EXPORTER_OTLP_ENDPOINT environment variable
        // (or configuration key) points at an OTLP collector. Without it, all traces and metrics are collected
        // in-process and then silently dropped — production deployments must set OTEL_EXPORTER_OTLP_ENDPOINT
        // to an OTLP-compatible endpoint (or uncomment the Azure Monitor block below) to retain observability.
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    // healthDetailRoles: roles allowed to read /health/detail. The caller (app layer) supplies its own role
    // taxonomy so this shared library stays auth-agnostic. When empty, any authenticated user is allowed.
    public static WebApplication MapDefaultEndpoints(this WebApplication app, params string[] healthDetailRoles)
    {
        // Public probes are intentionally body-light: orchestrators (Azure Web App, Supabase, k8s) only need the
        // HTTP status code, and we must not leak the internal dependency topology to anonymous callers. The
        // detailed per-check breakdown lives behind authorization on _healthDetailEndpointPath.

        // Liveness: only checks tagged "live" must pass. Must never touch external dependencies — a failing
        // liveness probe tells the host to *restart* the process, so a DB blip here would restart every instance.
        app.MapHealthChecks(_alivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteMinimalResponse
        });

        // Readiness: all checks (including dependency checks such as the database tagged "ready") must pass for
        // the app to receive traffic. A failure pulls the instance out of the load balancer without restarting it.
        app.MapHealthChecks(_healthEndpointPath, new HealthCheckOptions
        {
            ResponseWriter = WriteMinimalResponse
        });

        // Operator-facing detailed view: full per-check status/description/duration as JSON, including exception
        // messages — so it is restricted to the supplied operator roles (falling back to any authenticated user).
        var detail = app.MapHealthChecks(_healthDetailEndpointPath, new HealthCheckOptions
        {
            ResponseWriter = WriteDetailedJsonResponse
        });

        if (healthDetailRoles is { Length: > 0 })
            detail.RequireAuthorization(new AuthorizeAttribute { Roles = string.Join(',', healthDetailRoles) });
        else
            detail.RequireAuthorization();

        return app;
    }

    // Public probes return the aggregate status word only (e.g. "Healthy") alongside the HTTP status code.
    private static Task WriteMinimalResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());
    }

    // Authenticated detail endpoint: per-check breakdown for operators/dashboards.
    private static Task WriteDetailedJsonResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.ToString(),
                tags = entry.Value.Tags,
                error = entry.Value.Exception?.Message
            })
        };

        return context.Response.WriteAsJsonAsync(payload);
    }
}