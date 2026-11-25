using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Features.Transactions;

/// <summary>Answers every request (204 unless told otherwise) and records method, path and body for assertions.</summary>
internal sealed class RecordingHttpHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null) : HttpMessageHandler
{
    private readonly List<RecordedRequest> _requests = [];

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_requests)
                return [.. _requests];
        }
    }

    /// <summary>
    /// Serves what the entry forms load when they open (currencies, label catalog, bond definitions) and answers
    /// the owner save routes with <paramref name="saved"/>, so a form can run its whole save flow.
    /// </summary>
    public static RecordingHttpHandler ForEntryForms(
        IReadOnlyList<FinancialLabel>? labels = null, IReadOnlyList<BondDetails>? bonds = null, object? saved = null) => new(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/Currency/GetAll" => Json(new List<Currency> { DefaultCurrency.PLN, DefaultCurrency.USD }),
            "/api/FinancialLabel/get-count" => Json(labels?.Count ?? 0),
            "/api/FinancialLabel/get-by-index-and-count" => Json(labels ?? []),
            "/api/BondDetails" => Json(bonds ?? []),
            var path when path.StartsWith("/api/transactions/", StringComparison.Ordinal) =>
                saved is null ? new HttpResponseMessage(HttpStatusCode.NoContent) : Json(saved),
            _ => Json(Array.Empty<int>())
        });

    private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    public HttpClient CreateClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("http://localhost/") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (_requests)
            _requests.Add(new(request.Method, request.RequestUri!.PathAndQuery, body));
        return respond?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    internal sealed record RecordedRequest(HttpMethod Method, string PathAndQuery, string Body);
}