using FinanceManager.Api.Controllers.Accounts;
using FinanceManager.Application.FinancialAccounts.Bond;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class BondAccountControllerTests
{
    private readonly Mock<IAccountRepository<BondAccount>> _mockBondAccountRepository = new();
    private readonly Mock<IBondAccountEntryRepository<BondAccountEntry>> _mockBondAccountEntryRepository = new();
    private readonly Mock<IUserPlanVerifier> _userPlanVerifier = new();
    private readonly Mock<IAccountCsvExportService<BondAccountExportDto>> _bondAccountCsvExportService = new();
    private readonly BondAccountController _controller;

    public BondAccountControllerTests()
    {
        _controller = new BondAccountController(
            _mockBondAccountRepository.Object,
            _mockBondAccountEntryRepository.Object,
            new BondEntryProvider(_mockBondAccountEntryRepository.Object),
            _userPlanVerifier.Object,
            _bondAccountCsvExportService.Object);

        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new(ClaimTypes.NameIdentifier, "1")
        ], "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };
    }

    [Fact]
    public async Task ExportCsv_ReturnsFileResult_WithCsvContent()
    {
        var userId = 1;
        var accountId = 1;
        var startDate = new DateTime(2026, 1, 1);
        var endDate = new DateTime(2026, 1, 31);
        var csvContent = "PostingDate,ValueChange,Bond\n";
        BondAccount account = new(userId, accountId, "Bond Account", AccountLabel.Other);

        _mockBondAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _bondAccountCsvExportService
            .Setup(s => s.GetExportResults(userId, accountId, startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvContent);

        var result = await _controller.ExportCsv(accountId, startDate, endDate, TestContext.Current.CancellationToken);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Equal(csvContent, content);
    }

    [Fact]
    public async Task GetRecentEntries_ReturnsOkResult_WithEntriesAndOlderMetadata()
    {
        var userId = 1;
        var accountId = 1;
        var date = new DateTime(2026, 4, 26);
        BondAccount account = new(userId, accountId, "Bond Account", AccountLabel.Other);
        List<BondAccountEntry> entries =
        [
            new(accountId, 2, new DateTime(2026, 4, 20), 10500m, 500m, 101),
            new(accountId, 1, new DateTime(2026, 4, 10), 10000m, 10000m, 101),
        ];

        _mockBondAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockBondAccountEntryRepository.Setup(repo => repo.Get(accountId, date, 2, true)).ReturnsAsync(entries);
        _mockBondAccountEntryRepository
            .Setup(repo => repo.GetNextOlder(accountId, new DateTime(2026, 4, 10)))
            .ReturnsAsync(new Dictionary<int, BondAccountEntry> { [101] = new(accountId, 3, new DateTime(2026, 3, 1), 9500m, -500m, 101) });
        _mockBondAccountEntryRepository
            .Setup(repo => repo.GetNextYounger(accountId, new DateTime(2026, 4, 20)))
            .ReturnsAsync(new Dictionary<int, BondAccountEntry>());

        var result = await _controller.Get(accountId, date, 2, true);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<BondAccountDto>(okResult.Value);
        Assert.Equal(accountId, returnValue.AccountId);
        Assert.Equal(2, returnValue.Entries.Count());
        Assert.NotNull(returnValue.NextOlderEntries);
        Assert.True(returnValue.NextOlderEntries.ContainsKey(101));
    }

    [Fact]
    public async Task Get_WithMinimumEntryCount_BackfillsOlderEntriesUntilMinimumIsReached()
    {
        var (accountId, startDate, endDate) = SetupBackfillScenario();

        var result = await _controller.Get(accountId, startDate, endDate, minimumEntryCount: 2);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<BondAccountDto>(okResult.Value);
        Assert.Equal(2, returnValue.Entries.Count());
        Assert.Equal([2, 1], returnValue.Entries.Select(x => x.EntryId));
    }

    [Fact]
    public async Task GetInitialTransactionHistory_BackfillsOlderEntriesUntilMinimumIsReached()
    {
        var (accountId, startDate, endDate) = SetupBackfillScenario();

        var result = await _controller.GetInitialTransactionHistory(accountId, startDate, endDate, minimumEntryCount: 2);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnValue = Assert.IsType<BondAccountDto>(okResult.Value);
        Assert.Equal(2, returnValue.Entries.Count());
        Assert.Equal([2, 1], returnValue.Entries.Select(x => x.EntryId));
    }

    private (int AccountId, DateTime StartDate, DateTime EndDate) SetupBackfillScenario()
    {
        var userId = 1;
        var accountId = 1;
        var startDate = new DateTime(2026, 4, 1);
        var endDate = new DateTime(2026, 4, 30);
        var expandedStartDate = new DateTime(2026, 3, 15);
        BondAccount account = new(userId, accountId, "Bond Account", AccountLabel.Other);
        List<BondAccountEntry> initialEntries =
        [
            new(accountId, 2, new DateTime(2026, 4, 20), 10500m, 500m, 101),
        ];
        List<BondAccountEntry> expandedEntries =
        [
            new(accountId, 2, new DateTime(2026, 4, 20), 10500m, 500m, 101),
            new(accountId, 1, expandedStartDate, 10000m, 10000m, 101),
        ];

        _mockBondAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);
        _mockBondAccountEntryRepository.Setup(repo => repo.Get(accountId, startDate, endDate)).Returns(initialEntries.ToAsyncEnumerable());
        _mockBondAccountEntryRepository
            .Setup(repo => repo.GetNextOlder(accountId, new DateTime(2026, 4, 20)))
            .ReturnsAsync(new Dictionary<int, BondAccountEntry> { [101] = new(accountId, 1, expandedStartDate, 10000m, 10000m, 101) });
        _mockBondAccountEntryRepository.Setup(repo => repo.Get(accountId, expandedStartDate, endDate)).Returns(expandedEntries.ToAsyncEnumerable());
        _mockBondAccountEntryRepository
            .Setup(repo => repo.GetNextOlder(accountId, expandedStartDate))
            .ReturnsAsync(new Dictionary<int, BondAccountEntry>());
        _mockBondAccountEntryRepository
            .Setup(repo => repo.GetNextYounger(accountId, endDate))
            .ReturnsAsync(new Dictionary<int, BondAccountEntry>());

        return (accountId, startDate, endDate);
    }

    [Fact]
    public async Task GetInitialTransactionHistory_ReturnsBadRequest_WhenDateRangeIsInvalid()
    {
        var accountId = 1;
        BondAccount account = new(1, accountId, "Bond Account", AccountLabel.Other);
        _mockBondAccountRepository.Setup(repo => repo.Get(accountId)).ReturnsAsync(account);

        var result = await _controller.GetInitialTransactionHistory(accountId, new DateTime(2026, 5, 1), new DateTime(2026, 4, 1));

        Assert.IsType<BadRequestObjectResult>(result);
    }
}