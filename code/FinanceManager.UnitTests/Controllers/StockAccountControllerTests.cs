using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Providers;
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
    private readonly Mock<IAccountEntryRepository<StockAccountEntry>> _mockStockAccountEntryRepository;
    private readonly Mock<AccountIdProvider> _mockAccountIdProvider;
    private readonly StockAccountController _controller;

    public StockAccountControllerTests()
    {
        _mockStockAccountRepository = new Mock<IAccountRepository<StockAccount>>();
        _mockStockAccountEntryRepository = new Mock<IAccountEntryRepository<StockAccountEntry>>();
        _mockAccountIdProvider = new Mock<AccountIdProvider>(_mockStockAccountRepository.Object, new Mock<IAccountRepository<BankAccount>>().Object);
        _controller = new StockAccountController(_mockStockAccountRepository.Object, _mockAccountIdProvider.Object, _mockStockAccountEntryRepository.Object);

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
        _mockStockAccountRepository.Setup(repo => repo.GetAvailableAccounts(userId)).Returns(accounts);

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
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).Returns(account);

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
        _mockStockAccountRepository.Setup(repo => repo.Add(newAccountId, userId, addAccount.accountName)).Returns(newAccountId);

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
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).Returns(account);
        _mockStockAccountRepository.Setup(repo => repo.Delete(accountId)).Returns(true);

        // Act
        var result = await _controller.Delete(deleteAccount);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.True((bool)okResult.Value);
    }
}
