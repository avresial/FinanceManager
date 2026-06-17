using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Application.FinancialAccounts.Stock;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Exports;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class StockAccountControllerTests
{
    private readonly Mock<IAccountRepository<StockAccount>> _mockStockAccountRepository;
    private readonly Mock<IStockAccountEntryRepository<StockAccountEntry>> _mockStockAccountEntryRepository;
    private readonly Mock<IAccountCsvExportService<StockAccountExportDto>> _mockStockAccountCsvExportService;
    private readonly StockAccountController _controller;
    private const int _testUserId = 1;

    public StockAccountControllerTests()
    {
        _mockStockAccountRepository = new Mock<IAccountRepository<StockAccount>>();
        _mockStockAccountEntryRepository = new Mock<IStockAccountEntryRepository<StockAccountEntry>>();
        _mockStockAccountCsvExportService = new Mock<IAccountCsvExportService<StockAccountExportDto>>();
        _controller = new StockAccountController(
            _mockStockAccountRepository.Object,
            _mockStockAccountEntryRepository.Object,
            _mockStockAccountCsvExportService.Object);

        // Mock user identity
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new(ClaimTypes.NameIdentifier, _testUserId.ToString()),
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
        _mockStockAccountRepository.Setup(repo => repo.GetAvailableAccounts(_testUserId))
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
        _mockStockAccountRepository.Setup(repo => repo.GetAvailableAccounts(_testUserId))
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
        StockAccount account = new(_testUserId, accountId, "Test Account");
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
        _mockStockAccountRepository.Setup(repo => repo.Add(_testUserId, addAccount.AccountName))
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
        StockAccount account = new(_testUserId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockStockAccountRepository.Setup(repo => repo.Delete(accountId)).ReturnsAsync(true);
        _mockStockAccountEntryRepository.Setup(repo => repo.Delete(accountId)).ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(deleteAccount.AccountId);

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
        var result = await _controller.Delete(deleteAccount.AccountId);

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
        var result = await _controller.Delete(deleteAccount.AccountId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ExportCsv_ReturnsFileResult_WithCsvContent()
    {
        var accountId = 1;
        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 1, 31);
        var csvContent = "PostingDate,ValueChange,Ticker,InvestmentType\n";
        StockAccount account = new(_testUserId, accountId, "Test Account");

        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockStockAccountCsvExportService
            .Setup(s => s.GetExportResults(_testUserId, accountId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvContent);

        var result = await _controller.ExportCsv(accountId, startDate, endDate, TestContext.Current.CancellationToken);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Equal(csvContent, content);
    }

    [Fact]
    public async Task GetWithDateRange_BackfillsOlderEntriesUntilMinimumIsReached()
    {
        var (accountId, startDate, endDate) = SetupBackfillScenario();

        var result = await _controller.Get(accountId, startDate, endDate, minimumEntryCount: 2);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<StockAccountDto>(okResult.Value);
        Assert.Equal(2, returnValue.Entries.Count());
        Assert.Equal([2, 1], returnValue.Entries.Select(x => x.EntryId));
    }

    private (int AccountId, DateTime StartDate, DateTime EndDate) SetupBackfillScenario()
    {
        var accountId = 1;
        var startDate = new DateTime(2026, 4, 1);
        var endDate = new DateTime(2026, 4, 30);
        var expandedStartDate = new DateTime(2026, 3, 15);
        StockAccount account = new(_testUserId, accountId, "Test Account");
        List<StockAccountEntry> expandedEntries =
        [
            new(accountId, 2, new DateTime(2026, 4, 20), 10500m, 500m, "AAPL", InvestmentType.Stock),
            new(accountId, 1, expandedStartDate, 10000m, 10000m, "AAPL", InvestmentType.Stock),
        ];

        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockStockAccountEntryRepository
            .Setup(repo => repo.GetEntriesWithMinimumCount(accountId, startDate, endDate, 2))
            .ReturnsAsync((expandedEntries, expandedStartDate));
        _mockStockAccountEntryRepository
            .Setup(repo => repo.GetNextOlder(accountId, new DateTime(2026, 4, 20)))
            .ReturnsAsync(new Dictionary<string, StockAccountEntry> { ["AAPL"] = new(accountId, 1, expandedStartDate, 10000m, 10000m, "AAPL", InvestmentType.Stock) });
        _mockStockAccountEntryRepository
            .Setup(repo => repo.GetNextOlder(accountId, expandedStartDate))
            .ReturnsAsync(new Dictionary<string, StockAccountEntry>());
        _mockStockAccountEntryRepository
            .Setup(repo => repo.GetNextYounger(accountId, endDate))
            .ReturnsAsync(new Dictionary<string, StockAccountEntry>());

        return (accountId, startDate, endDate);
    }

    [Fact]
    public async Task GetWithDateRange_ReturnsBadRequest_WhenDateRangeIsInvalid()
    {
        var accountId = 1;
        StockAccount account = new(_testUserId, accountId, "Test Account");
        _mockStockAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        var result = await _controller.Get(accountId, new DateTime(2026, 5, 1), new DateTime(2026, 4, 1));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}