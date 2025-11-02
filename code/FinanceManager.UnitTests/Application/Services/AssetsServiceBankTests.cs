using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class AssetsServiceBankTests
{
    private readonly Mock<IFinancialAccountRepository> _financialAccountRepositoryMock = new();
    private readonly DateTime _start = new(DateTime.UtcNow.Year, 1, 1);
    private readonly DateTime _end = new(DateTime.UtcNow.Year, 1, 31);
    private readonly AssetsServiceBank _assetsServiceBank;

    public AssetsServiceBankTests() => _assetsServiceBank = new(_financialAccountRepositoryMock.Object);

    [Fact]
    public async Task IsAnyAccountWithAssets_ReturnsTrue_WhenRepositoryHasAccountWithAssets()
    {
        // arrange
        var account = new BankAccount(1, 1, "acct-a", AccountLabel.Cash);
        account.Add(new BankAccountEntry(1, 1, _end, 10, 0), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // act
        var result = await _assetsServiceBank.IsAnyAccountWithAssets(1);

        // assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetEndAssetsPerAccount_YieldsValues_FromRepository()
    {
        // arrange
        var account = new BankAccount(1, 1, "bank-a", AccountLabel.Cash);
        account.Add(new BankAccountEntry(1, 1, _end, 15, 0), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // act
        var list = await _assetsServiceBank.GetEndAssetsPerAccount(1, new Currency(0, "PLN", "PLN"), _end).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // assert
        Assert.Single(list);
        Assert.Equal("bank-a", list[0].Name);
        Assert.Equal(15m, list[0].Value);
    }

    [Fact]
    public async Task GetEndAssetsPerType_AggregatesByAccountType()
    {
        // arrange
        var account1 = new BankAccount(1, 1, "bank-a", AccountLabel.Cash);
        account1.Add(new BankAccountEntry(1, 1, _end, 10, 0), false);

        var account2 = new BankAccount(1, 2, "bank-b", AccountLabel.Cash);
        account2.Add(new BankAccountEntry(1, 1, _end, 5, 0), false);

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account1, account2 }.ToAsyncEnumerable());

        // act
        var results = await _assetsServiceBank.GetEndAssetsPerType(1, new Currency(0, "PLN", "PLN"), _end).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // assert
        Assert.Single(results);
        Assert.Equal(AccountLabel.Cash.ToString(), results[0].Name);
        Assert.Equal(15m, results[0].Value);
    }

    [Fact]
    public async Task GetAssetsTimeSeries_ReturnsTimeSeriesAcrossRange()
    {
        // arrange
        var account = new BankAccount(1, 1, "bank-a", AccountLabel.Cash);
        account.Add(new BankAccountEntry(1, 1, _start, 10, 0), false);
        account.Add(new BankAccountEntry(1, 2, _end, 0, 20));

        _financialAccountRepositoryMock.Setup(x => x.GetAccounts<BankAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
        .Returns(new[] { account }.ToAsyncEnumerable());

        // act
        var series = await _assetsServiceBank.GetAssetsTimeSeries(1, new Currency(0, "PLN", "PLN"), _start, _end);

        // assert
        Assert.NotEmpty(series);
        Assert.Contains(series, s => s.DateTime == _end && s.Value > 0);
    }
}
