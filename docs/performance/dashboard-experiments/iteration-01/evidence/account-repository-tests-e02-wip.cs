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

/// <summary>
/// Tests for the account repository's metadata branching (issue #458 / E02). <c>GetAccounts&lt;T&gt;</c> keys its
/// read cache on <c>includeEntryMetadata</c> and routes currency/bond range reads through either
/// <see cref="IAccountEntryRepository.GetRange"/> (with metadata) or <see cref="IAccountEntryRepository.GetValueRange"/>
/// (values only). These verify that routing is exact, and that dropping metadata leaves the balances and
/// bond identifiers unchanged — which is what NetWorthService relies on when it reads graphs for only values.
/// </summary>
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

    // ── E02 query count: routing is exact per includeEntryMetadata ───────────────────────

    [Fact]
    public async Task GetAccounts_CurrencyIncludeMetadata_True_UsesGetRange_NotGetValueRange()
    {
        var currencyAccounts = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();

        currencyAccounts.Setup(repository => repository.GetAll(It.IsAny<int>())).ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetNextOlder(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetNextYounger(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var repository = new AccountRepository(
            currencyAccounts.Object, Mock.Of<IAccountRepository<InvestmentAccount>>, Mock.Of<IAccountRepository<BondAccount>>,
            currencyEntries.Object, Mock.Of<IBondAccountEntryRepository<BondAccountEntry>>());

        await repository.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), includeEntryMetadata: true)
            .ToListAsync(TestContext.Current.CancellationToken);

        currencyEntries.Verify(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        currencyEntries.Verify(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task GetAccounts_CurrencyIncludeMetadata_False_UsesGetValueRange_NotGetRange()
    {
        var currencyAccounts = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();

        currencyAccounts.Setup(repository => repository.GetAll(It.IsAny<int>())).ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetNextOlder(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        currencyEntries.Setup(repository => repository.GetNextYounger(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        var repository = new AccountRepository(
            currencyAccounts.Object, Mock.Of<IAccountRepository<InvestmentAccount>>, Mock.Of<IAccountRepository<BondAccount>>,
            currencyEntries.Object, Mock.Of<IBondAccountEntryRepository<BondAccountEntry>>());

        await repository.GetAccounts<CurrencyAccount>(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), includeEntryMetadata: false)
            .ToListAsync(TestContext.Current.CancellationToken);

        currencyEntries.Verify(repository => repository.GetValueRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        currencyEntries.Verify(repository => repository.GetRange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    // ── E02 semantic parity: dropping metadata leaves balances and posting dates unchanged ────

    [Fact]
    public async Task GetAccounts_Currency_BothSettings_ProduceIdenticalBalancesAndPostingDates()
    {
        const int userId = 7;
        const int accountId = 11;
        var jan5 = new DateTime(2024, 1, 5);
        var jan10 = new DateTime(2024, 1, 10);
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 2, 1);

        var currencyAccounts = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        currencyAccounts.Setup(repository => repository.GetAll(userId)).ReturnsAsync([new CurrencyAccount(userId, accountId, "Cash", AccountLabel.Cash)]);

        // GetRange carries metadata (Description); GetValueRange strips it but keeps the same core fields.
        var withMetadata = [new CurrencyAccountEntry(accountId, 1, jan5, 100m, 100m) { Description = "Rent" }, new CurrencyAccountEntry(accountId, 2, jan10, 50m, 50m)];
        var withoutMetadata = [new CurrencyAccountEntry(accountId, 1, jan5, 100m, 100m), new CurrencyAccountEntry(accountId, 2, jan10, 50m, 50m)];

        var currencyEntries = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();
        currencyEntries.Setup(repository => repository.GetRange([accountId], start, end)).ReturnsAsync(withMetadata);
        currencyEntries.Setup(repository => repository.GetValueRange([accountId], start, end)).ReturnsAsync(withoutMetadata);
        currencyEntries.Setup(repository => repository.GetNextOlder([accountId], start)).ReturnsAsync(new Dictionary<int, CurrencyAccountEntry>());
        currencyEntries.Setup(repository => repository.GetNextYounger([accountId], end)).ReturnsAsync(new Dictionary<int, CurrencyAccountEntry>());

        var repository = new AccountRepository(
            currencyAccounts.Object, Mock.Of<IAccountRepository<InvestmentAccount>>, Mock.Of<IAccountRepository<BondAccount>>,
            currencyEntries.Object, Mock.Of<IBondAccountEntryRepository<BondAccountEntry>>());

        var withLabels = await repository.GetAccounts<CurrencyAccount>(userId, start, end, includeEntryMetadata: true).ToListAsync(TestContext.Current.CancellationToken);
        var withoutLabels = await repository.GetAccounts<CurrencyAccount>(userId, start, end, includeEntryMetadata: false).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(withLabels.Count, withoutLabels.Count);
        for (var i = 0; i < withLabels.Count; i++)
        {
            Assert.Equal(withLabels[i].EntryId, withoutLabels[i].EntryId);
            Assert.Equal(withLabels[i].PostingDate, withoutLabels[i].PostingDate);
            Assert.Equal(withLabels[i].Value, withoutLabels[i].Value);
            // Metadata differs on purpose — only the balance-defining fields must match.
            Assert.NotEqual(string.Empty, withLabels[i].Description);
        }
    }

    // ── E02 semantic parity: dropping metadata leaves bond values and identifiers unchanged ────

    [Fact]
    public async Task GetAccounts_Bond_BothSettings_ProduceIdenticalValuesAndBondIds()
    {
        const int userId = 9;
        const int accountId = 23;
        var jan5 = new DateTime(2024, 1, 5);
        var jan10 = new DateTime(2024, 1, 10);
        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 2, 1);

        var bondAccounts = new Mock<IAccountRepository<BondAccount>>();
        bondAccounts.Setup(repository => repository.GetAll(userId)).ReturnsAsync([new BondAccount(userId, accountId, "Bond", [], AccountLabel.Bond)]);

        // Same two entries both ways; GetRange carries metadata-free bonds that GetValueRange strips nothing on.
        var withMetadata = [new BondAccountEntry(accountId, 1, jan5, 1000m, 1000m, 301), new BondAccountEntry(accountId, 2, jan10, 1050m, 50m, 301)];
        var withoutMetadata = [new BondAccountEntry(accountId, 1, jan5, 1000m, 1000m, 301), new BondAccountEntry(accountId, 2, jan10, 1050m, 50m, 301)];

        var bondEntries = new Mock<IBondAccountEntryRepository<BondAccountEntry>>();
        bondEntries.Setup(repository => repository.GetRange([accountId], start, end)).ReturnsAsync(withMetadata);
        bondEntries.Setup(repository => repository.GetValueRange([accountId], start, end)).ReturnsAsync(withoutMetadata);
        bondEntries.Setup(repository => repository.GetNextOlderPerInstrument([accountId], start)).ReturnsAsync(new Dictionary<int, Dictionary<int, BondAccountEntry>>());
        bondEntries.Setup(repository => repository.GetNextYoungerPerInstrument([accountId], end)).ReturnsAsync(new Dictionary<int, Dictionary<int, BondAccountEntry>>());

        var repository = new AccountRepository(
            Mock.Of<ICurrencyAccountRepository<CurrencyAccount>>, Mock.Of<IAccountRepository<InvestmentAccount>>,
            bondAccounts.Object, Mock.Of<IAccountEntryRepository<CurrencyAccountEntry>>,
            bondEntries.Object);

        var withMetadataResult = await repository.GetAccounts<BondAccount>(userId, start, end, includeEntryMetadata: true).ToListAsync(TestContext.Current.CancellationToken);
        var withoutMetadataResult = await repository.GetAccounts<BondAccount>(userId, start, end, includeEntryMetadata: false).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(withMetadataResult.Count, withoutMetadataResult.Count);
        for (var i = 0; i < withMetadataResult.Count; i++)
        {
            var entryWith = withMetadataResult[i].Entries[i];
            var entryWithout = withoutMetadataResult[i].Entries[i];
            Assert.Equal(entryWith.EntryId, entryWithout.EntryId);
            Assert.Equal(entryWith.PostingDate, entryWithout.PostingDate);
            Assert.Equal(entryWith.Value, entryWithout.Value);
            Assert.Equal(entryWith.BondDetailsId, entryWithout.BondDetailsId);
        }
    }
}
