using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Imports;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.UnitTests.Controllers;

public class BankAccountControllerTests
{
    private readonly Mock<IBankAccountRepository<BankAccount>> _mockBankAccountRepository;
    private readonly Mock<IAccountEntryRepository<BankAccountEntry>> _mockBankAccountEntryRepository;

    private readonly Mock<IUserRepository> _userRepository;
    private readonly Mock<IUserPlanVerifier> _userPlanVerifier;
    private readonly Mock<IBankAccountImportService> _mockImportService;

    private readonly BankAccountController _controller;

    public BankAccountControllerTests()
    {
        _mockBankAccountRepository = new();
        _mockBankAccountEntryRepository = new();

        _userRepository = new Mock<IUserRepository>();
        var user = new User() { Login = "TestUser", UserId = 1, PricingLevel = Domain.Enums.PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        _userRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(user);

        _userPlanVerifier = new Mock<IUserPlanVerifier>();

        _mockImportService = new Mock<IBankAccountImportService>();

        _controller = new(_mockBankAccountRepository.Object, _mockBankAccountEntryRepository.Object, _userPlanVerifier.Object, _mockImportService.Object);

        // Mock user identity
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new (ClaimTypes.NameIdentifier, user.UserId.ToString())
        ], "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };
    }

    [Fact]
    public async Task Get_ReturnsOkResult_WithAccounts()
    {
        // Arrange
        var userId = 1;
        List<AvailableAccount> accounts = [new(1, "Test Account")];
        _mockBankAccountRepository.Setup(repo => repo.GetAvailableAccounts(userId)).Returns(accounts.ToAsyncEnumerable());

        // Act
        var result = await _controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<AvailableAccount>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task Get_WithAccountId_ReturnsOkResult_WithAccount()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        BankAccount account = new(userId, accountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        // Act
        var result = await _controller.Get(accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<BankAccount>(okResult.Value);
        Assert.Equal(accountId, returnValue.AccountId);
    }

    [Fact]
    public async Task Get_NoEntriesWithinDates_ReturnsOkResult_WithOlderThanLoaded()
    {
        // Arrange
        DateTime startDate = new(2000, 1, 1);
        DateTime endDate = new(2000, 2, 1);
        DateTime olderThanLoadedDate = startDate.AddYears(-1);
        DateTime youngerThanLoadedDate = endDate.AddYears(1);

        var userId = 1;
        var accountId = 1;
        BankAccount account = new(userId, accountId, "Test Account");
        BankAccountEntry bankAccountEntry = new(accountId, 1, olderThanLoadedDate, 1, 0);

        _mockBankAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockBankAccountEntryRepository.Setup(repo => repo.Get(accountId, startDate, endDate)).Returns(new List<BankAccountEntry>().ToAsyncEnumerable());
        _mockBankAccountEntryRepository.Setup(repo => repo.GetNextOlder(accountId, startDate))
            .ReturnsAsync(new BankAccountEntry(accountId, 1, olderThanLoadedDate, 1, 0));

        _mockBankAccountEntryRepository.Setup(repo => repo.GetNextYounger(accountId, endDate))
            .ReturnsAsync(new BankAccountEntry(accountId, 1, youngerThanLoadedDate, 1, 0));

        // Act
        var result = await _controller.Get(accountId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<BankAccountDto>(okResult.Value);
        Assert.Equal(accountId, returnValue.AccountId);
        Assert.NotNull(returnValue.NextOlderEntry);
        Assert.Equal(olderThanLoadedDate, returnValue.NextOlderEntry.PostingDate);

        Assert.NotNull(returnValue.NextYoungerEntry);
        Assert.Equal(youngerThanLoadedDate, returnValue.NextYoungerEntry.PostingDate);
    }

    [Fact]
    public async Task Add_ReturnsOkResult_WithNewAccount()
    {
        // Arrange
        var userId = 1;
        AddAccount addAccount = new("New Account");
        var newAccountId = 1;
        _mockBankAccountRepository.Setup(repo => repo.Add(userId, addAccount.accountName)).ReturnsAsync(newAccountId);
        _userPlanVerifier.Setup(x => x.CanAddMoreAccounts(userId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Add(addAccount);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(newAccountId, okResult.Value);
    }

    [Fact]
    public async Task AddEntry_ReturnsOkResult_WithNewEntry()
    {
        // Arrange
        AddBankAccountEntry addEntry = new(1, 1, DateTime.Now, 100, 0, "");
        BankAccountEntry bankAccountEntry = new(addEntry.AccountId, addEntry.EntryId, addEntry.PostingDate, addEntry.Value, addEntry.ValueChange)
        {
            Description = addEntry.Description,
        };

        _mockBankAccountEntryRepository.Setup(repo => repo.Add(It.IsAny<BankAccountEntry>())).ReturnsAsync(true);

        _userPlanVerifier.Setup(x => x.CanAddMoreEntries(1, 1)).ReturnsAsync(true);

        // Act
        var result = await _controller.AddEntry(addEntry);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Update_ReturnsOkResult_WithUpdatedAccount()
    {
        // Arrange
        var userId = 1;
        UpdateAccount updateAccount = new(1, "Updated Account");
        BankAccount account = new(userId, updateAccount.accountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(updateAccount.accountId)).ReturnsAsync(account);
        _mockBankAccountRepository.Setup(repo => repo.Update(updateAccount.accountId, updateAccount.accountName)).ReturnsAsync(true);

        // Act
        var result = await _controller.Update(updateAccount);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value);
    }

    [Fact]
    public async Task Delete_ReturnsOkResult_WithDeletedAccount()
    {
        // Arrange
        var userId = 1;
        DeleteAccount deleteAccount = new(1);
        BankAccount account = new(userId, deleteAccount.accountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(deleteAccount.accountId)).ReturnsAsync(account);
        _mockBankAccountRepository.Setup(repo => repo.Delete(deleteAccount.accountId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(deleteAccount.accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value);
    }

    [Fact]
    public async Task ImportBankEntries_ReturnsOk_WithImportedCount()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;

        var account = new BankAccount(userId, accountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        List<BankEntryImportRecordDto> entries =
        [
            new (DateTime.UtcNow.Date, 100m),
            new (DateTime.UtcNow.Date.AddDays(1), 200m)
        ];

        var importDto = new BankDataImportDto(accountId, entries);

        // Make repository Add succeed for each entry

        _mockImportService.Setup(x => x.ImportEntries(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<BankEntryImport>>())).ReturnsAsync(new ImportResult(1, entries.Count, 0, [], []));
        _mockBankAccountEntryRepository.Setup(repo => repo.Add(It.IsAny<BankAccountEntry>())).ReturnsAsync(true);

        // Act
        var result = await _controller.ImportBankEntries(importDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var value = okResult.Value!;
        var importedProp = value.GetType().GetProperty("Imported");
        var failedProp = value.GetType().GetProperty("Failed");

        Assert.NotNull(importedProp);
        Assert.NotNull(failedProp);

        var imported = (int)importedProp.GetValue(value)!;
        var failed = (int)failedProp.GetValue(value)!;

        Assert.Equal(entries.Count, imported);
        Assert.Equal(0, failed);
    }
}
