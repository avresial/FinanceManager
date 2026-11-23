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

    private static string? FileName(ContentDispositionHeaderValue? disposition) =>
        (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
}