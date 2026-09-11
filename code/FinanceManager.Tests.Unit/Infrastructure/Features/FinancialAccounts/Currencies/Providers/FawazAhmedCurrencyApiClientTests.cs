using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Providers;
using FinanceManager.Tests.Unit.Shared.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Currencies.Providers;

[Collection("Infrastructure")]
[Trait("Category", "Unit")]
public class FawazAhmedCurrencyApiClientTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _eur = new(2, "EUR", "€");
    private static readonly DateTime _date = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static FawazAhmedCurrencyApiClient CreateProvider() =>
        new(
            new HttpClient(new NeverCalledHandler()),
            NullLogger<FawazAhmedCurrencyApiClient>.Instance,
            new FakeDateTimeProvider(_date.AddDays(31)));

    [Fact]
    public async Task CancellablePoint_ThrowsWhenTokenIsAlreadyCancelled()
    {
        ICurrencyExchangeRateProvider provider = CreateProvider();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetExchangeRateAsync(
            _usd,
            _eur,
            _date,
            new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task CancellableRange_ThrowsWhenTokenIsAlreadyCancelled()
    {
        ICurrencyExchangeRateProvider provider = CreateProvider();

        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetExchangeRateAsync(
            _usd,
            _eur,
            _date,
            _date.AddDays(1),
            new CancellationToken(canceled: true)));
    }

    private sealed class NeverCalledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The pre-cancelled request must not reach the HTTP handler.");
    }
}