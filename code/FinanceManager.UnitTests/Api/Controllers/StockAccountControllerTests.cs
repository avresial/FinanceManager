using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.UnitTests.Api.Controllers;

public class StockAccountControllerTests
{
    private readonly Mock<IAccountRepository<StockAccount>> _mockStockAccountRepository;
    private readonly Mock<IStockAccountEntryRepository<StockAccountEntry>> _mockStockAccountEntryRepository;
    private readonly StockAccountController _controller;
    private const int TestUserId = 1;

    public StockAccountControllerTests()
    {
        _mockStockAccountRepository = new Mock<IAccountRepository<StockAccount>>();
        _mockStockAccountEntryRepository = new Mock<IStockAccountEntryRepository<StockAccountEntry>>();
        _controller = new StockAccountController(_mockStockAccountRepository.Object, _mockStockAccountEntryRepository.Object);

        // Mock user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new(ClaimTypes.NameIdentifier, TestUserId.ToString()),
        ], "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetAllAccounts_ReturnsNotFound_WhenNoAccounts()
    {
        // Arrange
        _mockStockAccountRepository.Setup(repo => repo.GetAvailableAccounts(TestUserId))
            .Returns(new List<AvailableAccount>().ToAsyncEnumerable());

        // Act
        var result = await _controller.Get();

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAllAccounts_ReturnsOkResult_WithAccounts()
    {
        // Arrange
        List<AvailableAccount> accounts = [new(1, "Test Account")];
        _mockStockAccountRepository.Setup(repo => repo.GetAvailableAccounts(TestUserId))
            .Returns(accounts.ToAsyncEnumerable());

        // Act
        var result = await _controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnValue = Assert.IsType<List<AvailableAccount>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetAccount_ReturnsOkResult_WithAccount()
    {
        // Arrange
        var accountId = 1;
        StockAccount account = new(TestUserId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        // Act
        var result = await _controller.Get(accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<StockAccount>(okResult.Value);
        Assert.Equal(accountId, returnValue.AccountId);
    }

    [Fact]
    public async Task GetAccount_ReturnsForbid_WhenUserDoesNotOwnAccount()
    {
        // Arrange
        var accountId = 1;
        var differentUserId = 999;
        StockAccount account = new(differentUserId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        // Act
        var result = await _controller.Get(accountId);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetAccount_ReturnsNoContent_WhenAccountNotFound()
    {
        // Arrange
        var accountId = 1;
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync((StockAccount?)null);

        // Act
        var result = await _controller.Get(accountId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task AddAccount_ReturnsOkResult_WithNewAccount()
    {
        // Arrange
        var newAccountId = 1;
        AddAccount addAccount = new("New Account");
        _mockStockAccountRepository.Setup(repo => repo.Add(TestUserId, addAccount.accountName))
            .ReturnsAsync(newAccountId);

        // Act
        var result = await _controller.Add(addAccount);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(newAccountId, okResult.Value);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsOkResult_WithSuccess()
    {
        // Arrange
        var accountId = 1;
        DeleteAccount deleteAccount = new(accountId);
        StockAccount account = new(TestUserId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockStockAccountRepository.Setup(repo => repo.Delete(accountId)).ReturnsAsync(true);
        _mockStockAccountEntryRepository.Setup(repo => repo.Delete(accountId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(deleteAccount.accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value!);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsForbid_WhenUserDoesNotOwnAccount()
    {
        // Arrange
        var accountId = 1;
        var differentUserId = 999;
        DeleteAccount deleteAccount = new(accountId);
        StockAccount account = new(differentUserId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        // Act
        var result = await _controller.Delete(deleteAccount.accountId);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsNotFound_WhenAccountDoesNotExist()
    {
        // Arrange
        var accountId = 1;
        DeleteAccount deleteAccount = new(accountId);
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync((StockAccount?)null);

        // Act
        var result = await _controller.Delete(deleteAccount.accountId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }
}