using FinanceManager.Application.Services.Currencies;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class AssetsServiceCurrencyTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly DateTime _start = new(DateTime.UtcNow.Year - 1, 1, 1);
    private readonly DateTime _end = new(DateTime.UtcNow.Year - 1, 1, 31);
    private readonly AssetsServiceCurrency _assetsServiceCurrency;

    public AssetsServiceCurrencyTests() => _assetsServiceCurrency = new(_financialAccountRepositoryMock.Object);

    [Fact]
    public async Task IsAnyAccountWithAssets_ReturnsTrue_WhenRepositoryHasAccountWithAssets()
    {
        // arrange
        var account = new CurrencyAccount(1, 1, "acct-a", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _end, 10, 0), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // act
        var result = await _assetsServiceCurrency.IsAnyAccountWithAssets(1);

        // assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_YieldsValues_FromRepository()
    {
        // arrange
        var account = new CurrencyAccount(1, 1, "currency-a", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _end, 15, 0), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // act
        var list = await _assetsServiceCurrency.GetEndAssetsPerAccount(1, new Currency(0, "PLN", "PLN"), _end).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // assert
        Assert.Single(list);
        Assert.Equal("currency-a", list[0].Name);
        Assert.Equal(15m, list[0].Value);
    }

    [Fact]
    public async Task GetEndAssetsPerType_AggregatesByAccountType()
    {
        // arrange
        var account1 = new CurrencyAccount(1, 1, "currency-a", AccountLabel.Cash);
        account1.Add(new CurrencyAccountEntry(1, 1, _end, 10, 0), false);

        var account2 = new CurrencyAccount(1, 2, "currency-b", AccountLabel.Cash);
        account2.Add(new CurrencyAccountEntry(1, 1, _end, 5, 0), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        // act
        var results = await _assetsServiceCurrency.GetEndAssetsPerType(1, new Currency(0, "PLN", "PLN"), _end).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // assert
        Assert.Single(results);
        Assert.Equal(AccountLabel.Cash.ToString(), results[0].Name);
        Assert.Equal(15m, results[0].Value);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsTimeSeriesAcrossRange()
    {
        // arrange
        var account = new CurrencyAccount(1, 1, "currency-a", AccountLabel.Cash);
        account.Add(new CurrencyAccountEntry(1, 1, _start, 10, 0), false);
        account.Add(new CurrencyAccountEntry(1, 2, _end, 0, 20));

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // act
        var series = await _assetsServiceCurrency.GetAssetsTimeSeries(1, new Currency(0, "PLN", "PLN"), _start, _end);

        // assert
        Assert.NotEmpty(series);
        Assert.Contains(series, s => s.DateTime == _end && s.Value > 0);
    }
}