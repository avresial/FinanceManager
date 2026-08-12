using Microsoft.Extensions.Logging;

namespace ServiceDefaults;

/// <summary>
/// Adds a final, redacted log record around every HttpClient configured by ServiceDefaults.
/// </summary>
public sealed class ExternalDependencyLoggingHandler(
    ILogger<ExternalDependencyLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var logLevel = ExternalDependencyLogging.IsExpectedNotFound(request, response.StatusCode)
                    ? LogLevel.Information
                    : LogLevel.Warning;

                logger.Log(
                    logLevel,
                    "External dependency request returned a non-success response. Service: {Service}; Host: {Host}; Method: {Method}; Path: {Path}; Result: {StatusCode}; Reason: {ReasonPhrase}.",
                    ExternalDependencyLogging.GetService(request),
                    ExternalDependencyLogging.GetHost(request),
                    ExternalDependencyLogging.GetMethod(request),
                    ExternalDependencyLogging.GetPath(request),
                    (int)response.StatusCode,
                    ExternalDependencyLogging.RedactText(response.ReasonPhrase));
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-requested cancellation is not a dependency failure.
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                ExternalDependencyLogging.RedactException(exception),
                "External dependency request failed. Service: {Service}; Host: {Host}; Method: {Method}; Path: {Path}.",
                ExternalDependencyLogging.GetService(request),
                ExternalDependencyLogging.GetHost(request),
                ExternalDependencyLogging.GetMethod(request),
                ExternalDependencyLogging.GetPath(request));

            throw;
        }
    }

}