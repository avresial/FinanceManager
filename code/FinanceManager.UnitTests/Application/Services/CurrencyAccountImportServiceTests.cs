using FinanceManager.Application.Services;
using FinanceManager.Application.Services.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.UnitTests.Application.Services;

public class CurrencyAccountImportServiceTests
{
    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _mockAccountRepository;
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _mockAccountEntryRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly UserPlanVerifier _userPlanVerifier;
    private readonly CurrencyAccountImportService _service;

    public CurrencyAccountImportServiceTests()
    {
        _mockAccountRepository = new Mock<ICurrencyAccountRepository<CurrencyAccount>>();
        _mockAccountEntryRepository = new Mock<IAccountEntryRepository<CurrencyAccountEntry>>();

        _mockUserRepository = new Mock<IUserRepository>();
        var user = new User() { Login = "TestUser", UserId = 1, PricingLevel = PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        _mockUserRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(user);

        // create a real UserPlanVerifier so its CanAddMoreEntries method can be used in test
        _userPlanVerifier = new UserPlanVerifier(_mockAccountRepository.Object, _mockAccountEntryRepository.Object, _mockUserRepository.Object);

        var mockLogger = new Mock<ILogger<CurrencyAccountImportService>>();
        _service = new CurrencyAccountImportService(_mockAccountRepository.Object, _mockAccountEntryRepository.Object, _userPlanVerifier, mockLogger.Object);
    }

    [Fact]
    public async Task ImportEntries_ImportsAll_WhenRepositorySucceeds()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);

        // Ensure user plan allows importing by returning no accounts / zero used entries
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(new List<AvailableAccount>().ToAsyncEnumerable());

        List<CurrencyEntryImport> domainEntries =
        [
            new (DateTime.UtcNow.Date, 100m),
            new (DateTime.UtcNow.Date.AddDays(1), 200m)
        ];

        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>())).Returns(new List<CurrencyAccountEntry>().ToAsyncEnumerable());
        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<bool>())).ReturnsAsync(true);

        // Act
        var result = await _service.ImportEntries(userId, accountId, domainEntries);

        // Assert
        Assert.Equal(accountId, result.AccountId);
        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
    }
}