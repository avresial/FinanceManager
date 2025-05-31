using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Providers;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Providers;
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
    private readonly Mock<AccountIdProvider> _mockAccountIdProvider;

    private readonly Mock<IUserRepository> _userRepository;
    private readonly Mock<UserPlanVerifier> _userPlanVerifier;

    private readonly BankAccountController _controller;

    public BankAccountControllerTests()
    {
        _mockBankAccountRepository = new Mock<IBankAccountRepository<BankAccount>>();
        _mockBankAccountEntryRepository = new Mock<IAccountEntryRepository<BankAccountEntry>>();
        _mockAccountIdProvider = new Mock<AccountIdProvider>(new Mock<IAccountRepository<StockAccount>>().Object, _mockBankAccountRepository.Object);

        _userRepository = new Mock<IUserRepository>();
        var user = new User() { Login = "TestUser", UserId = 1, PricingLevel = Domain.Enums.PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        _userRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(user);
        _userPlanVerifier = new Mock<UserPlanVerifier>(_mockBankAccountRepository.Object, _mockBankAccountEntryRepository.Object, _userRepository.Object, new PricingProvider());
        _controller = new BankAccountController(_mockBankAccountRepository.Object, _mockAccountIdProvider.Object, _mockBankAccountEntryRepository.Object, _userPlanVerifier.Object);

        // Mock user identity
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString())
        }, "mock"));

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
        var accounts = new List<AvailableAccount> { new(1, "Test Account") };
        _mockBankAccountRepository.Setup(repo => repo.GetAvailableAccounts(userId)).ReturnsAsync(accounts);

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
        var account = new BankAccount(userId, accountId, "Test Account");
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
        var account = new BankAccount(userId, accountId, "Test Account");
        BankAccountEntry bankAccountEntry = new(accountId, 1, olderThanLoadedDate, 1, 0);

        _mockBankAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockBankAccountEntryRepository.Setup(repo => repo.Get(accountId, startDate, endDate)).ReturnsAsync(new List<BankAccountEntry>());
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
        Assert.Equal(olderThanLoadedDate, returnValue.OlderThanLoadedEntry);
        Assert.Equal(youngerThanLoadedDate, returnValue.YoungerThanLoadedEntry);
    }

    [Fact]
    public async Task Add_ReturnsOkResult_WithNewAccount()
    {
        // Arrange
        var userId = 1;
        var addAccount = new AddAccount("New Account");
        var newAccountId = 1;
        _mockBankAccountRepository.Setup(repo => repo.Add(newAccountId, userId, addAccount.accountName)).ReturnsAsync(newAccountId);

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
        var addEntry = new AddBankAccountEntry(new BankAccountEntry(1, 1, DateTime.Now, 100, 0));
        _mockBankAccountEntryRepository.Setup(repo => repo.Add(addEntry.entry)).ReturnsAsync(true);

        // Act
        var result = await _controller.AddEntry(addEntry);

        // Assert
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);

        Assert.True(((Task<bool>)okResult.Value).Result);
    }

    [Fact]
    public async Task Update_ReturnsOkResult_WithUpdatedAccount()
    {
        // Arrange
        var userId = 1;
        var updateAccount = new UpdateAccount(1, "Updated Account");
        var account = new BankAccount(userId, updateAccount.accountId, "Test Account");
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
        var deleteAccount = new DeleteAccount(1);
        var account = new BankAccount(userId, deleteAccount.accountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(deleteAccount.accountId)).ReturnsAsync(account);
        _mockBankAccountRepository.Setup(repo => repo.Delete(deleteAccount.accountId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(deleteAccount.accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value);
    }
}
