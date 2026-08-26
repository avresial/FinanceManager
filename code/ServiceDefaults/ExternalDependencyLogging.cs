using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace ServiceDefaults;

internal static class ExternalDependencyLogging
{
    private static readonly Regex _queryParameterRegex = new(
        @"(?<prefix>[?&][^#\s&=]+)=(?<value>[^#\s&]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex _secretAssignmentRegex = new(
        @"(?<prefix>\b(?:api[-_]?key|access[-_]?token|refresh[-_]?token|client[-_]?secret|password|secret|token)\b\s*[=:]\s*)(?<value>[^\s,;&]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex _authorizationValueRegex = new(
        @"(?<prefix>\b(?:bearer|basic)\s+)(?<value>[^\s,;&]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string GetService(HttpRequestMessage? request)
    {
        var host = request?.RequestUri?.Host;
        if (string.IsNullOrWhiteSpace(host))
            return "Unknown external dependency";

        return host.ToLowerInvariant() switch
        {
            "www.alphavantage.co" => "Alpha Vantage",
            "api.twelvedata.com" => "Twelve Data",
            "api.openfigi.com" => "OpenFIGI",
            "api.nbp.pl" => "NBP",
            "data-api.ecb.europa.eu" => "ECB",
            "cdn.jsdelivr.net" => "Fawaz Ahmed currency API",
            "ec.europa.eu" => "Eurostat",
            "openrouter.ai" => "OpenRouter",
            "api.openai.com" => "OpenAI",
            "api.github.com" => "GitHub",
            "models.inference.ai.azure.com" => "GitHub Models",
            "eodhd.com" => "EODHD",
            _ when host.EndsWith(".twelvedata.com", StringComparison.OrdinalIgnoreCase) => "Twelve Data",
            _ when host.EndsWith(".eodhd.com", StringComparison.OrdinalIgnoreCase) => "EODHD",
            _ when host.EndsWith(".openfigi.com", StringComparison.OrdinalIgnoreCase) => "OpenFIGI",
            _ when host.EndsWith(".nbp.pl", StringComparison.OrdinalIgnoreCase) => "NBP",
            _ when host.EndsWith(".ecb.europa.eu", StringComparison.OrdinalIgnoreCase) => "ECB",
            _ when host.EndsWith(".ec.europa.eu", StringComparison.OrdinalIgnoreCase) => "Eurostat",
            _ => host
        };
    }

    public static string GetHost(HttpRequestMessage? request) =>
        request?.RequestUri?.Host ?? "(unknown)";

    public static string GetMethod(HttpRequestMessage? request) =>
        request?.Method.Method ?? "UNKNOWN";

    // Keep the operation key useful for resilience telemetry without including
    // request-specific values such as query strings, paths, or correlation IDs.
    public static string GetOperationKey(HttpRequestMessage? request) =>
        $"{GetService(request)} {GetMethod(request)} {GetHost(request)}";

    // Query strings are intentionally excluded because provider URLs can contain API keys.
    public static string GetPath(HttpRequestMessage? request) =>
        RedactText(request?.RequestUri?.AbsolutePath ?? "(unknown)");

    public static bool IsExpectedNotFound(HttpRequestMessage? request, HttpStatusCode statusCode)
    {
        if (statusCode != HttpStatusCode.NotFound)
            return false;

        var host = request?.RequestUri?.Host;
        return host is not null &&
            (host.Equals("api.nbp.pl", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".nbp.pl", StringComparison.OrdinalIgnoreCase)
                || host.Equals("data-api.ecb.europa.eu", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".ecb.europa.eu", StringComparison.OrdinalIgnoreCase)
                || host.Equals("cdn.jsdelivr.net", StringComparison.OrdinalIgnoreCase));
    }

    public static string RedactText(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = _queryParameterRegex.Replace(value, "${prefix}=[REDACTED]");
        redacted = _secretAssignmentRegex.Replace(redacted, "${prefix}[REDACTED]");
        return _authorizationValueRegex.Replace(redacted, "${prefix}[REDACTED]");
    }

    public static Exception RedactException(Exception exception) =>
        new RedactedException(RedactText(exception.ToString()));

    private sealed class RedactedException(string details) : Exception(details)
    {
        public override string ToString() => Message;
    }
}