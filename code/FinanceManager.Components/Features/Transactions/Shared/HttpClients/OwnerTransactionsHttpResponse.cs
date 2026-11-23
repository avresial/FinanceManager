using FinanceManager.Components.Features.Transactions.Shared.Models;
using System.Net.Http.Headers;

namespace FinanceManager.Components.Features.Transactions.Shared.HttpClients;

internal static class OwnerTransactionsHttpResponse
{
    /// <summary>Throws with the server's explanation as the message when the call did not succeed.</summary>
    public static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body, inner: null, response.StatusCode);
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