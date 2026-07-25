using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Repositories;
using Moq;

namespace FinanceManager.Tests.Unit.Infrastructure.Repositories;

[Trait("Category", "Unit")]
public class AccountRepositoryTests
{
    [Fact]
    public async Task ReadScope_ReusesIdenticalAccountRangeOnlyUntilDisposed()
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

        using (repository.BeginReadScope())
        {
            await repository.GetAccounts<CurrencyAccount>(1, start, end).ToListAsync(TestContext.Current.CancellationToken);
            await repository.GetAccounts<CurrencyAccount>(1, start, end).ToListAsync(TestContext.Current.CancellationToken);
        }

        await repository.GetAccounts<CurrencyAccount>(1, start, end).ToListAsync(TestContext.Current.CancellationToken);

        currencyAccounts.Verify(value => value.GetAll(1), Times.Exactly(2));
    }
}