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

[Collection("Application")]
[Trait("Category", "Unit")]
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

    [Fact]
    public async Task ImportEntries_EmptyList_ReturnsZeroImported()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var entries = Array.Empty<CurrencyEntryImport>();

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(0, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task ImportEntries_NullEntries_ThrowsArgumentNullException()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ImportEntries(userId, accountId, null!));
    }

    [Fact]
    public async Task ImportEntries_ExceedsPlanLimit_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;

        // Set up user with Free plan (limit 1000)
        var user = new User() { Login = "TestUser", UserId = userId, PricingLevel = PricingLevel.Free, CreationDate = DateTime.UtcNow };
        _mockUserRepository.Setup(x => x.GetUser(userId)).ReturnsAsync(user);

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId))
            .Returns(new[] { new AvailableAccount(accountId, "Test") }.ToAsyncEnumerable());

        // Already at limit
        _mockAccountEntryRepository.Setup(x => x.GetCount(accountId)).ReturnsAsync(1000);

        var entries = new[] { new CurrencyEntryImport(DateTime.UtcNow, 100m, "Test") };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ImportEntries(userId, accountId, entries));

        Assert.Contains("Plan does not allow", exception.Message);
    }

    [Fact]
    public async Task ImportEntries_AccountNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = 1;
        var accountId = 999;
        var entries = new[] { new CurrencyEntryImport(DateTime.UtcNow, 100m, "Test") };

        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync((CurrencyAccount?)null);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ImportEntries(userId, accountId, entries));

        Assert.Contains("Account not found", exception.Message);
    }

    [Fact]
    public async Task ImportEntries_WrongUserId_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var entries = new[] { new CurrencyEntryImport(DateTime.UtcNow, 100m, "Test") };

        var account = new CurrencyAccount(2, accountId, "Wrong User Account"); // UserId = 2
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ImportEntries(userId, accountId, entries));

        Assert.Contains("access denied", exception.Message);
    }

    [Fact]
    public async Task ImportEntries_OutOfOrderDates_SortsBeforeImporting()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = new[]
        {
            new CurrencyEntryImport(date.AddDays(2), 300m, "Third"),
            new CurrencyEntryImport(date, 100m, "First"),
            new CurrencyEntryImport(date.AddDays(1), 200m, "Second")
        };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        var addedEntries = new List<CurrencyAccountEntry>();
        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<bool>()))
            .Callback<CurrencyAccountEntry, bool>((entry, _) => addedEntries.Add(entry))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(3, result.Imported);
        // Service processes from newest to oldest, so first added should be newest
        Assert.Equal(date.AddDays(2), addedEntries[0].PostingDate.Date);
        Assert.Equal(date.AddDays(1), addedEntries[1].PostingDate.Date);
        Assert.Equal(date, addedEntries[2].PostingDate.Date);
    }

    [Fact]
    public async Task ImportEntries_ExactMatch_CreatesConflict()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = new[] { new CurrencyEntryImport(date, 100m, "Contractor1") };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        var existingEntry = new CurrencyAccountEntry(accountId, 1, date, 100m, 100m)
        {
            ContractorDetails = "Contractor1"
        };
        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { existingEntry }.ToAsyncEnumerable());

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(0, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Single(result.Conflicts);
        Assert.Contains("Exact match", result.Conflicts[0].Reason);
    }

    [Fact]
    public async Task ImportEntries_ImportMissingFromExisting_CreatesConflict()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = new[]
        {
            new CurrencyEntryImport(date, 100m, "Contractor1"),
            new CurrencyEntryImport(date, 100m, "Contractor2") // Duplicate amount, same date
        };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        // Only one existing entry, but two imports with same date/amount
        var existingEntry = new CurrencyAccountEntry(accountId, 1, date, 100m, 100m);
        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { existingEntry }.ToAsyncEnumerable());

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(0, result.Imported);
        Assert.True(result.Conflicts.Count > 0);
    }

    [Fact]
    public async Task ImportEntries_ExistingMissingFromImport_CreatesConflict()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = new[] { new CurrencyEntryImport(date, 100m, "Contractor1") };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        // Two existing entries, but only one import with same date/amount
        var existingEntry1 = new CurrencyAccountEntry(accountId, 1, date, 100m, 100m);
        var existingEntry2 = new CurrencyAccountEntry(accountId, 2, date, 100m, 100m);
        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(new[] { existingEntry1, existingEntry2 }.ToAsyncEnumerable());

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(0, result.Imported);
        Assert.True(result.Conflicts.Count > 0);
    }

    [Fact]
    public async Task ImportEntries_NonUtcDate_FailsWithError()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var nonUtcDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Local);
        var entries = new[] { new CurrencyEntryImport(nonUtcDate, 100m, "Contractor1") };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
        Assert.Contains("not UTC", result.Errors[0]);
    }

    [Fact]
    public async Task ImportEntries_RepositoryFailsToAdd_IncrementsFailed()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = new[] { new CurrencyEntryImport(date, 100m, "Contractor1") };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<bool>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.Single(result.Errors);
    }

    [Fact]
    public async Task ImportEntries_LargeBulkImport_HandlesCorrectly()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = Enumerable.Range(1, 500)
            .Select(i => new CurrencyEntryImport(date.AddDays(i), i * 10m, $"Contractor{i}"))
            .ToArray();

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(500, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportEntries_MultipleEntriesSameDay_ImportsAll()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = new[]
        {
            new CurrencyEntryImport(date, 100m, "Morning"),
            new CurrencyEntryImport(date, 200m, "Afternoon"),
            new CurrencyEntryImport(date, 300m, "Evening")
        };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Add(It.IsAny<CurrencyAccountEntry>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(3, result.Imported);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task ImportEntries_MixedSuccessAndFailure_TracksCorrectly()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var date = DateTime.UtcNow.Date;
        var entries = new[]
        {
            new CurrencyEntryImport(date, 100m, "Success1"),
            new CurrencyEntryImport(date, 200m, "Fail"),
            new CurrencyEntryImport(date, 300m, "Success2")
        };

        var account = new CurrencyAccount(userId, accountId, "Test");
        _mockAccountRepository.Setup(x => x.Get(accountId)).ReturnsAsync(account);
        _mockAccountRepository.Setup(x => x.GetAvailableAccounts(userId)).Returns(Array.Empty<AvailableAccount>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Get(accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<CurrencyAccountEntry>().ToAsyncEnumerable());

        _mockAccountEntryRepository.Setup(x => x.Add(It.Is<CurrencyAccountEntry>(e => e.ValueChange == 200m), It.IsAny<bool>()))
            .ReturnsAsync(false);
        _mockAccountEntryRepository.Setup(x => x.Add(It.Is<CurrencyAccountEntry>(e => e.ValueChange != 200m), It.IsAny<bool>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ImportEntries(userId, accountId, entries);

        // Assert
        Assert.Equal(2, result.Imported);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task ApplyResolvedConflicts_NullConflicts_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ApplyResolvedConflicts(null!));
    }

    [Fact]
    public async Task ApplyResolvedConflicts_DeleteExisting_CallsDelete()
    {
        // Arrange
        var resolvedConflict = new ResolvedImportConflict
        {
            AccountId = 1,
            ExistingId = 5,
            LeaveExisting = false,
            AddImported = false,
            ImportData = null
        };

        // Act
        await _service.ApplyResolvedConflicts(new[] { resolvedConflict });

        // Assert
        _mockAccountEntryRepository.Verify(x => x.Delete(1, 5), Times.Once);
        _mockAccountEntryRepository.Verify(x => x.Add(It.IsAny<CurrencyAccountEntry>()), Times.Never);
    }

    [Fact]
    public async Task ApplyResolvedConflicts_AddImported_CallsAdd()
    {
        // Arrange
        var importData = new CurrencyEntryImport(DateTime.UtcNow, 100m, "Test");
        var resolvedConflict = new ResolvedImportConflict
        {
            AccountId = 1,
            ExistingId = null,
            LeaveExisting = true,
            AddImported = true,
            ImportData = importData
        };

        // Act
        await _service.ApplyResolvedConflicts(new[] { resolvedConflict });

        // Assert
        _mockAccountEntryRepository.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockAccountEntryRepository.Verify(x => x.Add(It.IsAny<CurrencyAccountEntry>()), Times.Once);
    }

    [Fact]
    public async Task ApplyResolvedConflicts_BothDeleteAndAdd_ExecutesBoth()
    {
        // Arrange
        var importData = new CurrencyEntryImport(DateTime.UtcNow, 100m, "Test");
        var resolvedConflict = new ResolvedImportConflict
        {
            AccountId = 1,
            ExistingId = 5,
            LeaveExisting = false,
            AddImported = true,
            ImportData = importData
        };

        // Act
        await _service.ApplyResolvedConflicts(new[] { resolvedConflict });

        // Assert
        _mockAccountEntryRepository.Verify(x => x.Delete(1, 5), Times.Once);
        _mockAccountEntryRepository.Verify(x => x.Add(It.IsAny<CurrencyAccountEntry>()), Times.Once);
    }

    [Fact]
    public async Task ApplyResolvedConflicts_MultipleConflicts_ProcessesAll()
    {
        // Arrange
        var conflicts = new[]
        {
            new ResolvedImportConflict
            {
                AccountId = 1,
                ExistingId = 5,
                LeaveExisting = false,
                AddImported = false,
                ImportData = null
            },
            new ResolvedImportConflict
            {
                AccountId = 1,
                ExistingId = 6,
                LeaveExisting = false,
                AddImported = false,
                ImportData = null
            },
            new ResolvedImportConflict
            {
                AccountId = 1,
                ExistingId = null,
                LeaveExisting = true,
                AddImported = true,
                ImportData = new CurrencyEntryImport(DateTime.UtcNow, 100m, "Test")
            }
        };

        // Act
        await _service.ApplyResolvedConflicts(conflicts);

        // Assert
        _mockAccountEntryRepository.Verify(x => x.Delete(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
        _mockAccountEntryRepository.Verify(x => x.Add(It.IsAny<CurrencyAccountEntry>()), Times.Once);
    }
}