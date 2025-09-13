using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.UnitTests.Services;
public class DuplicateEntryResolverServiceTests
{
    private readonly Mock<IBankAccountRepository<BankAccount>> _bankAccountRepositoryMock = new();
    private readonly Mock<IAccountEntryRepository<BankAccountEntry>> _accountEntryRepositoryMock = new();
    private readonly Mock<IDuplicateEntryRepository> _duplicateEntryRepository = new();

    private DuplicateEntryResolverService _duplicateEntryResolverService;

    public DuplicateEntryResolverServiceTests()
    {
        _bankAccountRepositoryMock.Setup(x => x.Exists(1)).ReturnsAsync(true);
        List<BankAccountEntry> entries =
        [
            new (1, 1, new (2000, 1, 1), 100, 100),
            new (1, 2, new (2000, 1, 1), 200, 100),
        ];

        _accountEntryRepositoryMock.Setup(x => x.Get(1, new(2000, 1, 1), new(2000, 1, 2))).ReturnsAsync(entries);
        _accountEntryRepositoryMock.Setup(x => x.GetOldest(1)).ReturnsAsync(entries.First());
        _duplicateEntryRepository.Setup(x => x.AddDuplicate(It.IsAny<IEnumerable<DuplicateEntry>>()));

        _duplicateEntryRepository.Setup(x => x.GetDuplicateByEntry(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync((DuplicateEntry?)null);

        _duplicateEntryResolverService = new DuplicateEntryResolverService(_bankAccountRepositoryMock.Object,
            _accountEntryRepositoryMock.Object, _duplicateEntryRepository.Object);
    }

    [Fact]
    public async Task GetsDuplicate()
    {
        // Arrange

        // Act
        await _duplicateEntryResolverService.Scan(1);

        // Assert
        _duplicateEntryRepository.Verify(x => x.AddDuplicate(It.IsAny<IEnumerable<DuplicateEntry>>()), Times.Once);
    }
}
