using FinanceManager.Components.Features.Transactions.Shared.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FinanceManager.Components.Features.Transactions.Shared.HttpClients;

internal static class OwnerTransactionsHttpResponse
{
    /// <summary>Throws with the server's explanation as the message when the call did not succeed.</summary>
    public static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(ReadableMessage(body) ?? response.ReasonPhrase, inner: null, response.StatusCode);
    }

    // The API answers with a plain string, a ProblemDetails (detail) or a ValidationProblem (errors); show the explanation, not the JSON.
    private static string? ReadableMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String) return root.GetString();
            if (root.ValueKind != JsonValueKind.Object) return body;
            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String) return detail.GetString();
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                foreach (var field in errors.EnumerateObject())
                    foreach (var message in field.Value.EnumerateArray())
                        if (message.ValueKind == JsonValueKind.String) return message.GetString();
            return root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String ? title.GetString() : null;
        }
        catch (JsonException)
        {
            return body;
        }
    }

    public static async Task<CsvDownload> ReadCsv(HttpResponseMessage response, string fallbackFileName, CancellationToken cancellationToken)
    {
        await EnsureSuccess(response, cancellationToken);
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new CsvDownload(content, FileName(response.Content.Headers.ContentDisposition) ?? fallbackFileName);
    }

    /// <summary>Formats a date as an invariant UTC query value; an unspecified kind is taken as UTC.</summary>
    public static string Utc(DateTime date) => (date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime())
        .ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture);

    private static string? FileName(ContentDispositionHeaderValue? disposition) =>
        (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
}