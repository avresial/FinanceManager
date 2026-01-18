using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.UnitTests.Api.Controllers;

public class BankAccountControllerTests
{
    private readonly Mock<ICurrencyAccountRepository<CurrencyAccount>> _mockBankAccountRepository = new();
    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _mockBankAccountEntryRepository = new();

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserPlanVerifier> _userPlanVerifier = new();

    private readonly BankAccountController _controller;

    public BankAccountControllerTests()
    {
        var user = new User() { Login = "TestUser", UserId = 1, PricingLevel = PricingLevel.Premium, CreationDate = DateTime.UtcNow };
        _userRepository.Setup(x => x.GetUser(It.IsAny<int>())).ReturnsAsync(user);

        _controller = new(_mockBankAccountRepository.Object, _mockBankAccountEntryRepository.Object, _userPlanVerifier.Object);

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
        CurrencyAccount account = new(userId, accountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        // Act
        var result = await _controller.Get(accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<CurrencyAccount>(okResult.Value);
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
        CurrencyAccount account = new(userId, accountId, "Test Account");
        CurrencyAccountEntry bankAccountEntry = new(accountId, 1, olderThanLoadedDate, 1, 0);

        _mockBankAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockBankAccountEntryRepository.Setup(repo => repo.Get(accountId, startDate, endDate)).Returns(new List<CurrencyAccountEntry>().ToAsyncEnumerable());
        _mockBankAccountEntryRepository.Setup(repo => repo.GetNextOlder(accountId, startDate))
            .ReturnsAsync(new CurrencyAccountEntry(accountId, 1, olderThanLoadedDate, 1, 0));

        _mockBankAccountEntryRepository.Setup(repo => repo.GetNextYounger(accountId, endDate))
            .ReturnsAsync(new CurrencyAccountEntry(accountId, 1, youngerThanLoadedDate, 1, 0));

        // Act
        var result = await _controller.Get(accountId, startDate, endDate);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<CurrencyAccountDto>(okResult.Value);
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
    public async Task Update_ReturnsOkResult_WithUpdatedAccount()
    {
        // Arrange
        var userId = 1;
        UpdateAccount updateAccount = new(1, "Updated Account", AccountLabel.Cash);
        CurrencyAccount account = new(userId, updateAccount.AccountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(updateAccount.AccountId)).ReturnsAsync(account);
        _mockBankAccountRepository.Setup(repo => repo.Update(updateAccount.AccountId, updateAccount.AccountName, AccountLabel.Cash)).ReturnsAsync(true);

        // Act
        var result = await _controller.Update(updateAccount);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<bool>(okResult.Value);
        Assert.True((bool)okResult.Value);
    }

    [Fact]
    public async Task Delete_ReturnsOkResult_WithDeletedAccount()
    {
        // Arrange
        var userId = 1;
        DeleteAccount deleteAccount = new(1);
        CurrencyAccount account = new(userId, deleteAccount.accountId, "Test Account");
        _mockBankAccountRepository.Setup(repo => repo.Get(deleteAccount.accountId)).ReturnsAsync(account);
        _mockBankAccountRepository.Setup(repo => repo.Delete(deleteAccount.accountId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(deleteAccount.accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<bool>(okResult.Value);
        Assert.True((bool)okResult.Value);
    }
}