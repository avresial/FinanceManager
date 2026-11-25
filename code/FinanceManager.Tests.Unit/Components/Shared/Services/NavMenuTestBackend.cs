using Blazored.LocalStorage;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Unit.Components.Shared.Services;

/// <summary>Answers the three requests that build the nav menu state, and builds a <see cref="NavMenuStateCacheService"/> over them.</summary>
public sealed class NavMenuTestBackend(bool hasAssets = false, bool hasLiabilities = false, int transactionCount = 0, bool failTransactions = false) : HttpMessageHandler
{
    /// <summary>What the server answers from now on, so a test can commit data after the menu has loaded.</summary>
    public bool HasAssets { get; set; } = hasAssets;
    public bool HasLiabilities { get; set; } = hasLiabilities;
    public int TransactionCount { get; set; } = transactionCount;

    public List<string> Requests { get; } = [];
    public Mock<ILocalStorageService> Storage { get; } = new();

    public int TransactionRequests => Requests.Count(path => path.StartsWith("api/TransactionLog/Exists/", StringComparison.Ordinal));

    public NavMenuStateCacheService CreateService()
    {
        HttpClient Client() => new(this) { BaseAddress = new Uri("http://localhost/") };
        return new NavMenuStateCacheService(
            Storage.Object, new MemoryCache(new MemoryCacheOptions()), new TransactionLogHttpClient(Client()),
            new AssetsHttpClient(Client()), new LiabilitiesHttpClient(Client()), NullLogger<NavMenuStateCacheService>.Instance);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery.TrimStart('/');
        Requests.Add(path);
        if (path.StartsWith("api/Assets/IsAnyAccountWithAssets/", StringComparison.Ordinal)) return Json(HasAssets);
        if (path.StartsWith("api/Liabilities/IsAnyAccountWithLiabilities/", StringComparison.Ordinal)) return Json(HasLiabilities);
        if (path.StartsWith("api/TransactionLog/Exists/", StringComparison.Ordinal))
        {
            if (failTransactions) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Json(TransactionCount > 0);
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static Task<HttpResponseMessage> Json<T>(T value) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });
}