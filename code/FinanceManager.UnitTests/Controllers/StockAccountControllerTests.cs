using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.UnitTests.Controllers;
public class StockAccountControllerTests
{
    private readonly Mock<IAccountRepository<StockAccount>> _mockStockAccountRepository;
    private readonly Mock<IStockAccountEntryRepository<StockAccountEntry>> _mockStockAccountEntryRepository;
    private readonly StockAccountController _controller;

    public StockAccountControllerTests()
    {
        _mockStockAccountRepository = new Mock<IAccountRepository<StockAccount>>();
        _mockStockAccountEntryRepository = new Mock<IStockAccountEntryRepository<StockAccountEntry>>();
        _controller = new StockAccountController(_mockStockAccountRepository.Object, _mockStockAccountEntryRepository.Object);

        // Mock user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetAllAccounts_ReturnsOkResult_WithAccounts()
    {
        // Arrange
        var userId = 1;
        var accounts = new List<AvailableAccount> { new AvailableAccount(1, "Test Account") };
        _mockStockAccountRepository.Setup(repo => repo.GetAvailableAccounts(userId)).ReturnsAsync(accounts);

        // Act
        var result = await _controller.Get();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<List<AvailableAccount>>(okResult.Value);
        Assert.Single(returnValue);
    }

    [Fact]
    public async Task GetAccount_ReturnsOkResult_WithAccount()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var account = new StockAccount(userId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        // Act
        var result = await _controller.Get(accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<StockAccount>(okResult.Value);
        Assert.Equal(accountId, returnValue.AccountId);
    }

    [Fact]
    public async Task AddAccount_ReturnsOkResult_WithNewAccount()
    {
        // Arrange
        var userId = 1;
        var newAccountId = 1;
        var addAccount = new AddAccount("New Account");
        _mockStockAccountRepository.Setup(repo => repo.Add(newAccountId, userId, addAccount.accountName)).ReturnsAsync(newAccountId);

        // Act
        var result = await _controller.Add(addAccount);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(newAccountId, okResult.Value);
    }

    [Fact]
    public async Task DeleteAccount_ReturnsOkResult_WithDeletedAccount()
    {
        // Arrange
        var userId = 1;
        var accountId = 1;
        var deleteAccount = new DeleteAccount(accountId);
        var account = new StockAccount(userId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockStockAccountRepository.Setup(repo => repo.Delete(accountId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(deleteAccount.accountId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value);
    }
}
