using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class BankAccountImportServiceTests
{
    private readonly Mock<IBankAccountRepository<BankAccount>> _mockBankAccountRepository;
    private readonly Mock<IAccountEntryRepository<BankAccountEntry>> _mockBankAccountEntryRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly UserPlanVerifier _userPlanVerifier;
    private readonly BankAccountImportService _service;

    public BankAccountImportServiceTests()
    {
        _mockBankAccountRepository = new Mock<IBankAccountRepository<BankAccount>>();
        _mockBankAccountEntryRepository = new Mock<IAccountEntryRepository<BankAccountEntry>>();

        _mockUserRepository = new Mock<IUserRepository>();
        var user = new User() { Login = "TestUser", UserId = 1, PricingLevel = PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        _mockUserRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(user);

        // create a real UserPlanVerifier so its CanAddMoreEntries method can be used in test
        _userPlanVerifier = new UserPlanVerifier(_mockBankAccountRepository.Object, _mockBankAccountEntryRepository.Object, _mockUserRepository.Object);

        var mockLogger = new Mock<ILogger<BankAccountImportService>>();
        _service = new BankAccountImportService(_mockBankAccountRepository.Object, _mockBankAccountEntryRepository.Object, _userPlanVerifier, mockLogger.Object);
    }

    [Fact]
    public async Task ImportEntries_ImportsAll_WhenRepositorySucceeds()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var account = new BankAccount(userId, accountId, "Test");
        _mockBankAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        // Ensure user plan allows importing by returning no accounts / zero used entries
        _mockBankAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(new List<AvailableAccount>().ToAsyncEnumerable());

        List<BankEntryImport> domainEntries =
        [
            new (DateTime.UtcNow.Date, 100m),
            new (DateTime.UtcNow.Date.AddDays(1), 200m)
        ];

        _mockBankAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new List<BankAccountEntry>().ToAsyncEnumerable());
        _mockBankAccountEntryRepository.Setup(x => x.Add(It.IsAny<BankAccountEntry>(), It.IsAny<bool>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportEntries(userId, accountId, domainEntries);

        // Assert
        Assert.Equal(accountId, result.AccountId);
        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
    }
}