using FinanceManager.Infrastructure.Features.MoneyFlow.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.MoneyFlow.Providers;

[Trait("Category", "Unit")]
public class EurostatInflationIndexProviderTests
{
    [Fact]
    public async Task GetIndexSeries_MapsMonthlyValues_AndExtendsLatestValueToRangeEnd()
    {
        const string json = """
            {
              "value": { "0": 100.0, "1": 102.5 },
              "dimension": {
                "time": {
                  "category": {
                    "index": { "2026-01": 0, "2026-02": 1 }
                  }
                }
              }
            }
            """;
        using var httpClient = new HttpClient(new StubHandler(json))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var provider = new EurostatInflationIndexProvider(
            httpClient,
            NullLogger<EurostatInflationIndexProvider>.Instance);
        var start = new DateTime(2026, 1, 15);
        var end = new DateTime(2026, 2, 20);

        var series = await provider.GetIndexSeriesAsync(
            start,
            end,
            TestContext.Current.CancellationToken);

        Assert.Equal(100m, series[start]);
        Assert.Equal(102.5m, series[new DateTime(2026, 2, 1)]);
        Assert.Equal(102.5m, series[end]);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}