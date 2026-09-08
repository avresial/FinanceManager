using FinanceManager.Application.FinancialAccounts.Currencies.ExchangeRates;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Services;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Trait("Category", "Unit")]
public class StoredCurrencyExchangeRateSourceTests
{
    private static readonly Currency _usd = new(1, "USD", "$");
    private static readonly Currency _eur = new(2, "EUR", "€");

    [Fact]
    public async Task ResolveAsync_OverlappingCalls_AreSerializedWithinTheScopedSource()
    {
        var repository = new Mock<IExchangeRateRepository>();
        var inner = new Mock<ICurrencyExchangeRateSource>();
        var firstInnerCallEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstInnerCall = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeInnerCalls = 0;
        var maximumActiveInnerCalls = 0;

        repository
            .Setup(x => x.GetRange(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<(string From, string To, DateTime Date), decimal>());
        repository
            .Setup(x => x.Get(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((decimal?)null);
        repository
            .Setup(x => x.AddRange(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<(DateTime Date, decimal Rate)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        repository
            .Setup(x => x.Add(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        inner
            .Setup(x => x.ResolveAsync(
                It.IsAny<Currency>(),
                It.IsAny<Currency>(),
                It.IsAny<IReadOnlyCollection<DateTime>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<CurrencyExchangeRateResolutionContext>()))
            .Returns(async (Currency from, Currency to, IReadOnlyCollection<DateTime> dates, bool _, CancellationToken _, CurrencyExchangeRateResolutionContext? _) =>
            {
                var active = Interlocked.Increment(ref activeInnerCalls);
                Interlocked.Exchange(ref maximumActiveInnerCalls, Math.Max(maximumActiveInnerCalls, active));
                firstInnerCallEntered.TrySetResult();
                await releaseFirstInnerCall.Task;
                Interlocked.Decrement(ref activeInnerCalls);

                return dates.ToDictionary(
                    date => date,
                    date => new CurrencyExchangeRateResolution(
                        CurrencyExchangeRateResult.Success(0.92m),
                        CurrencyExchangeRateSource.Provider));
            });

        var sut = new StoredCurrencyExchangeRateSource(repository.Object, inner.Object);
        var cancellationToken = TestContext.Current.CancellationToken;
        var first = sut.ResolveAsync(
            _usd,
            _eur,
            [new DateTime(2024, 1, 15), new DateTime(2024, 1, 16)],
            false,
            cancellationToken);
        await firstInnerCallEntered.Task.WaitAsync(cancellationToken);

        var second = sut.ResolveAsync(_usd, _eur, [new DateTime(2024, 1, 17)], false, cancellationToken);
        releaseFirstInnerCall.TrySetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, maximumActiveInnerCalls);
    }
}