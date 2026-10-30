using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

[Trait("Category", "Unit")]
public class AccountRepositoryTests
{
    [Fact]
    public async Task GetAccounts_ReusesIdenticalRangeUntilAWrite()
    {
        var currencyAccounts = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        var account = new CurrencyAccount(1, 10, "Cash", AccountLabel.Cash);

        currencyAccounts.Setup(repository => repository.GetAll(1)).ReturnsAsync([account]);
        currencyEntries
            .Setup(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries
            .Setup(repository => repository.GetNextOlder(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries
            .Setup(repository => repository.GetNextYounger(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var repository = new AccountRepository(
            currencyAccounts.Object,
            Mock.Of<IAccountRepository<InvestmentAccount>>(),
            Mock.Of<IAccountRepository<BondAccount>>(),
            currencyEntries.Object,
            Mock.Of<IBondAccountEntryRepository<BondAccountEntry>>());
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 2, 1);

        await repository.GetAccounts<CurrencyAccount>(1, start, end).ToListAsync(TestContext.Current.CancellationToken);
        await repository.GetAccounts<CurrencyAccount>(1, start, end).ToListAsync(TestContext.Current.CancellationToken);
        await repository.RemoveAccount(typeof(CurrencyAccount), account.AccountId);
        await repository.GetAccounts<CurrencyAccount>(1, start, end).ToListAsync(TestContext.Current.CancellationToken);

        currencyAccounts.Verify(value => value.GetAll(1), Times.Exactly(2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAccounts_Currency_RoutesToMetadataRangeOrValueRange(bool includeEntryMetadata)
    {
        var currencyAccounts = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();

        var account = new CurrencyAccount(7, 11, "Cash", AccountLabel.Cash);
        List<CurrencyAccountEntry> rangeEntries =
        [
            new(11, 1, new DateTime(2024, 1, 5), 100m, 100m) { Description = "Opening deposit" },
            new(11, 2, new DateTime(2024, 1, 10), 150m, 50m) { Description = "Top up" }
        ];
        List<CurrencyAccountEntry> valueEntries =
        [
            new(11, 1, new DateTime(2024, 1, 5), 100m, 100m),
            new(11, 2, new DateTime(2024, 1, 10), 150m, 50m)
        ];

        currencyAccounts.Setup(repository => repository.GetAll(7)).ReturnsAsync([account]);
        currencyEntries
            .Setup(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rangeEntries);
        currencyEntries
            .Setup(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(valueEntries);
        currencyEntries
            .Setup(repository => repository.GetNextOlder(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<int, CurrencyAccountEntry>());
        currencyEntries
            .Setup(repository => repository.GetNextYounger(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<int, CurrencyAccountEntry>());

        var repository = new AccountRepository(
            currencyAccounts.Object,
            Mock.Of<IAccountRepository<InvestmentAccount>>(),
            Mock.Of<IAccountRepository<BondAccount>>(),
            currencyEntries.Object,
            Mock.Of<IBondAccountEntryRepository<BondAccountEntry>>());
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 2, 1);

        var result = await repository.GetAccounts<CurrencyAccount>(7, start, end, includeEntryMetadata).ToListAsync(TestContext.Current.CancellationToken);

        var resultAccount = Assert.Single(result);
        Assert.Equal(11, resultAccount.AccountId);

        var firstEntry = Assert.Single(resultAccount.Entries, entry => entry.EntryId == 1);
        Assert.Equal(new DateTime(2024, 1, 5), firstEntry.PostingDate);
        Assert.Equal(100m, firstEntry.Value);
        Assert.Equal(100m, firstEntry.ValueChange);

        var secondEntry = Assert.Single(resultAccount.Entries, entry => entry.EntryId == 2);
        Assert.Equal(new DateTime(2024, 1, 10), secondEntry.PostingDate);
        Assert.Equal(150m, secondEntry.Value);
        Assert.Equal(50m, secondEntry.ValueChange);

        await repository.GetAccounts<CurrencyAccount>(7, start, end, includeEntryMetadata).ToListAsync(TestContext.Current.CancellationToken);

        if (includeEntryMetadata)
        {
            currencyEntries.Verify(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Once());
            currencyEntries.Verify(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Never());
        }
        else
        {
            currencyEntries.Verify(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Never());
            currencyEntries.Verify(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Once());
        }

        currencyAccounts.Verify(repository => repository.GetAll(7), Times.Once());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAccounts_Bond_RoutesToMetadataRangeOrValueRange(bool includeEntryMetadata)
    {
        var bondAccounts = new Mock<IAccountRepository<BondAccount>>();
        var bondEntries = new Mock<IBondAccountEntryRepository<BondAccountEntry>>();

        var account = new BondAccount(7, 11, "Bonds", AccountLabel.Bond);
        List<BondAccountEntry> rangeEntries =
        [
            new(11, 1, new DateTime(2024, 1, 5), 100m, 100m, 301),
            new(11, 2, new DateTime(2024, 1, 10), 150m, 50m, 301)
        ];
        List<BondAccountEntry> valueEntries =
        [
            new(11, 1, new DateTime(2024, 1, 5), 100m, 100m, 301),
            new(11, 2, new DateTime(2024, 1, 10), 150m, 50m, 301)
        ];

        bondAccounts.Setup(repository => repository.GetAll(7)).ReturnsAsync([account]);
        bondEntries
            .Setup(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(rangeEntries);
        bondEntries
            .Setup(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(valueEntries);
        bondEntries
            .Setup(repository => repository.GetNextOlderPerInstrument(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<int, Dictionary<int, BondAccountEntry>>());
        bondEntries
            .Setup(repository => repository.GetNextYoungerPerInstrument(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<int, Dictionary<int, BondAccountEntry>>());

        var repository = new AccountRepository(
            Mock.Of<ICurrencyAccountRepository<CurrencyAccount>>(),
            Mock.Of<IAccountRepository<InvestmentAccount>>(),
            bondAccounts.Object,
            Mock.Of<IAccountEntryRepository<CurrencyAccountEntry>>(),
            bondEntries.Object);
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 2, 1);

        var result = await repository.GetAccounts<BondAccount>(7, start, end, includeEntryMetadata).ToListAsync(TestContext.Current.CancellationToken);

        var resultAccount = Assert.Single(result);
        Assert.Equal(11, resultAccount.AccountId);

        var firstEntry = Assert.Single(resultAccount.Entries, entry => entry.EntryId == 1);
        Assert.Equal(new DateTime(2024, 1, 5), firstEntry.PostingDate);
        Assert.Equal(100m, firstEntry.Value);
        Assert.Equal(100m, firstEntry.ValueChange);
        Assert.Equal(301, firstEntry.BondDetailsId);

        var secondEntry = Assert.Single(resultAccount.Entries, entry => entry.EntryId == 2);
        Assert.Equal(new DateTime(2024, 1, 10), secondEntry.PostingDate);
        Assert.Equal(150m, secondEntry.Value);
        Assert.Equal(50m, secondEntry.ValueChange);
        Assert.Equal(301, secondEntry.BondDetailsId);

        await repository.GetAccounts<BondAccount>(7, start, end, includeEntryMetadata).ToListAsync(TestContext.Current.CancellationToken);

        if (includeEntryMetadata)
        {
            bondEntries.Verify(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Once());
            bondEntries.Verify(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Never());
        }
        else
        {
            bondEntries.Verify(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Never());
            bondEntries.Verify(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), start, end), Times.Once());
        }

        bondAccounts.Verify(repository => repository.GetAll(7), Times.Once());
    }
}