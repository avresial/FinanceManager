using Blazored.LocalStorage;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Unit.Components.Features.Dashboard.Services;

[Trait("Category", "Unit")]
public class InvestmentRateCacheServiceTests
{
    private const int _userId = 4;
    private const int _currencyId = 0;
    private const string _storageKey = "investment-rate-cache-v1:4:0:2026-07-29";

    // Two visits to the assets page on the same day: the card stamps each context with the clock, so the
    // instants differ down to the tick even though the data behind them is the same.
    private static readonly DateTime _firstVisit = new DateTime(2026, 7, 29, 18, 59, 58, DateTimeKind.Utc).AddTicks(5_310_000);
    private static readonly DateTime _secondVisit = new DateTime(2026, 7, 29, 21, 4, 12, DateTimeKind.Utc).AddTicks(7_742_000);

    [Fact]
    public async Task SecondVisitOnTheSameDay_ReadsTheEntryTheFirstVisitWrote()
    {
        var localStorage = new Mock<ILocalStorageService>();
        var written = new Dictionary<string, InvestmentRateCacheSnapshot>();
        localStorage
            .Setup(s => s.SetItemAsync(It.IsAny<string>(), It.IsAny<InvestmentRateCacheSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<string, InvestmentRateCacheSnapshot, CancellationToken>((key, snapshot, _) => written[key] = snapshot);
        localStorage
            .Setup(s => s.GetItemAsync<InvestmentRateCacheSnapshot>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => written.GetValueOrDefault(key));
        localStorage
            .Setup(s => s.KeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => written.Keys.ToList());

        // Separate services with separate memory caches stand in for a page reload: only what reached
        // local storage can carry over.
        await CreateService(localStorage).GetSnapshotAsync(Context(_firstVisit));
        var snapshot = await CreateService(localStorage).GetSnapshotAsync(Context(_secondVisit));

        Assert.Equal([_storageKey], written.Keys);
        Assert.Equal(_firstVisit, snapshot.EndDateTime);
    }

    [Fact]
    public async Task VisitOnTheNextDay_SupersedesTheStoredEntry()
    {
        var localStorage = new Mock<ILocalStorageService>();
        localStorage
            .Setup(s => s.KeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_storageKey]);

        await CreateService(localStorage).GetSnapshotAsync(Context(_secondVisit.AddDays(1)));

        localStorage.Verify(
            s => s.SetItemAsync("investment-rate-cache-v1:4:0:2026-07-30", It.IsAny<InvestmentRateCacheSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Once);
        localStorage.Verify(
            s => s.RemoveItemsAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(new[] { _storageKey })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static InvestmentRateRefreshContext Context(DateTime endDateTime) => new()
    {
        UserId = _userId,
        CurrencyId = _currencyId,
        EndDateTime = endDateTime,
    };

    // The HTTP client is only reached when nothing usable is cached; it answers with an empty payload so
    // these tests stay about key shape rather than about rate maths.
    private static InvestmentRateCacheService CreateService(Mock<ILocalStorageService> localStorage) =>
        new(localStorage.Object,
            new MemoryCache(new MemoryCacheOptions()),
            new MoneyFlowHttpClient(new HttpClient(new EmptyJsonArrayHandler()) { BaseAddress = new Uri("http://localhost/") }),
            NullLogger<InvestmentRateCacheService>.Instance);

    private sealed class EmptyJsonArrayHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            });
    }
}