using FinanceManager.Api.Features.Dashboard.Controllers;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.FinancialAccounts.Shared;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Unit.Api.Features.Dashboard.Controllers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class TransactionLogControllerTests
{
    private const int _testUserId = 1;

    private readonly Mock<ITransactionLogService> _transactionLogService = new();
    private readonly TransactionLogController _controller;

    public TransactionLogControllerTests()
    {
        _controller = new TransactionLogController(_transactionLogService.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, _testUserId.ToString())], "mock"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task Page_PassesSearchAlongsideExistingFilters()
    {
        var day = new DateTime(2026, 10, 20);
        var query = new TransactionLogQuery(day, day, AccountTypes: [AccountType.Currency], AccountIds: [10], Search: "Café & bond");
        var page = new TransactionLogPage([], null);
        _transactionLogService.Setup(x => x.GetPage(_testUserId, It.Is<TransactionLogQuery>(q =>
            q.Search == query.Search && q.AccountTypes!.Contains(AccountType.Currency) && q.AccountIds!.Contains(10)),
            TestContext.Current.CancellationToken)).ReturnsAsync(page);
        var result = await _controller.Page(_testUserId, day, day, accountTypes: [AccountType.Currency], accountIds: [10],
            search: query.Search, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(page, Assert.IsType<OkObjectResult>(result).Value);
        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task ItemsPage_MapsEachItemKindAndTheCursor_ToTheWireContract()
    {
        var day = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var cursor = new TransactionLogCursor(day, AccountType.Bond, 2);
        _transactionLogService.Setup(x => x.GetItemsPage(_testUserId, It.Is<TransactionLogQuery>(q => q.Search == "x" && q.PageSize == 3),
            TestContext.Current.CancellationToken)).ReturnsAsync(new TransactionLogItemsPage(
        [
            new CurrencyTransactionLogItem(10, "Bank", 3, "EUR", new CurrencyAccountEntry(10, 1, day, 90, -10) { Description = "Lunch" }),
            new BondTransactionLogItem(20, "Bonds", new BondAccountEntry(20, 2, day, 5, 5, 7)),
            new InvestmentTransactionLogItem(30, "Broker", new InvestmentTransaction
            {
                EntryId = 3, AccountId = 30, TradeDate = DateOnly.FromDateTime(day), Quantity = 1, UnitPrice = 2, Currency = "USD"
            }),
        ], cursor));

        var result = await _controller.ItemsPage(_testUserId, day, day, pageSize: 3, search: "x",
            cancellationToken: TestContext.Current.CancellationToken);

        var page = Assert.IsType<TransactionLogItemsPageDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(new TransactionLogCursorDto(day, AccountTypeDto.Bond, 2), page.NextCursor);
        var cash = Assert.IsType<CurrencyTransactionLogItemDto>(page.Items[0]);
        Assert.Equal((10, "Bank", 3, "EUR", 1L, "Lunch"), (cash.AccountId, cash.AccountName, cash.CurrencyId, cash.Currency, cash.Entry.EntryId, cash.Entry.Description));
        Assert.Equal(7, Assert.IsType<BondTransactionLogItemDto>(page.Items[1]).Entry.BondDetailsId);
        Assert.Equal((3L, 30), (Assert.IsType<InvestmentTransactionLogItemDto>(page.Items[2]).Transaction.Id, page.Items[2].AccountId));
    }

    [Fact]
    public async Task ItemsPage_ForbidsOtherUsers_AndRejectsInvalidQueries()
    {
        var day = new DateTime(2026, 10, 20);

        Assert.IsType<ForbidResult>(await _controller.ItemsPage(_testUserId + 1, day, day, cancellationToken: TestContext.Current.CancellationToken));
        var invalid = await _controller.ItemsPage(_testUserId, day, day.AddDays(-1), pageSize: 0, cursorEntryId: 1,
            cancellationToken: TestContext.Current.CancellationToken);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(invalid);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problem = Assert.IsAssignableFrom<ValidationProblemDetails>(objectResult.Value);
        Assert.Contains("pageSize", problem.Errors.Keys);
        Assert.Contains("start", problem.Errors.Keys);
        Assert.Contains("cursorDate", problem.Errors.Keys);
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Get_ReturnsOk_ForAuthenticatedUser()
    {
        // Arrange
        List<TransactionLogEntryDto> transactions =
        [
            new(10, "Bank", AccountType.Currency, 1, new DateTime(2024, 3, 5), -50, "Groceries"),
        ];
        _transactionLogService.Setup(s => s.GetLastTransactions(_testUserId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactions);

        // Act
        var result = await _controller.Get(_testUserId, 10, TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(transactions, okResult.Value);
    }

    [Fact]
    public async Task Get_ReturnsForbid_WhenRequestingAnotherUsersData()
    {
        // Act
        var result = await _controller.Get(_testUserId + 1, 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<ForbidResult>(result);
        _transactionLogService.Verify(s => s.GetLastTransactions(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_ForOwner_PassesRangeCursorAndCancellation()
    {
        var day = new DateTime(2024, 3, 5);
        var cursor = new TransactionLogCursor(day, AccountType.Bond, 12);
        var query = new TransactionLogQuery(day.AddDays(-1), day, 2, cursor);
        var page = new TransactionLogPage([], null);
        _transactionLogService.Setup(s => s.GetPage(_testUserId, query, TestContext.Current.CancellationToken)).ReturnsAsync(page);

        var result = await _controller.Page(_testUserId, query.Start, query.End, 2,
            cursor.Date, cursor.AccountType, cursor.EntryId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(page, Assert.IsType<OkObjectResult>(result).Value);
        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task Page_WithFilters_PassesAccountTypesAndIdsToTheQuery()
    {
        var day = new DateTime(2024, 3, 5);
        var page = new TransactionLogPage([], null);
        _transactionLogService.Setup(s => s.GetPage(_testUserId, It.Is<TransactionLogQuery>(q =>
                q.AccountTypes!.SequenceEqual(new[] { AccountType.Bond, AccountType.Stock }) &&
                q.AccountIds!.SequenceEqual(new[] { 4, 9 })), TestContext.Current.CancellationToken))
            .ReturnsAsync(page);

        var result = await _controller.Page(_testUserId, day.AddDays(-1), day,
            accountTypes: [AccountType.Bond, AccountType.Stock], accountIds: [4, 9],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(page, Assert.IsType<OkObjectResult>(result).Value);
        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task Page_WithoutFilters_PassesNoFilterToTheQuery()
    {
        var day = new DateTime(2024, 3, 5);
        _transactionLogService.Setup(s => s.GetPage(_testUserId, It.Is<TransactionLogQuery>(q =>
                q.AccountTypes == null && q.AccountIds == null), TestContext.Current.CancellationToken))
            .ReturnsAsync(new TransactionLogPage([], null));

        await _controller.Page(_testUserId, day.AddDays(-1), day, cancellationToken: TestContext.Current.CancellationToken);

        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task Page_WithMaximumNumberOfAccountIds_IsAccepted()
    {
        var day = new DateTime(2024, 3, 5);
        _transactionLogService.Setup(s => s.GetPage(_testUserId, It.IsAny<TransactionLogQuery>(), TestContext.Current.CancellationToken))
            .ReturnsAsync(new TransactionLogPage([], null));

        var result = await _controller.Page(_testUserId, day.AddDays(-1), day,
            accountIds: [.. Enumerable.Range(1, 100)], cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Page_WithUndefinedAccountType_ReturnsValidationProblem()
    {
        var day = new DateTime(2024, 3, 5);

        var result = await _controller.Page(_testUserId, day.AddDays(-1), day,
            accountTypes: [AccountType.Currency, (AccountType)99], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Page_WithNonPositiveAccountId_ReturnsValidationProblem(int accountId)
    {
        var day = new DateTime(2024, 3, 5);

        var result = await _controller.Page(_testUserId, day.AddDays(-1), day,
            accountIds: [5, accountId], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Page_WithTooManyAccountIds_ReturnsValidationProblem()
    {
        var day = new DateTime(2024, 3, 5);

        var result = await _controller.Page(_testUserId, day.AddDays(-1), day,
            accountIds: [.. Enumerable.Range(1, 101)], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Page_ForOtherUser_DoesNotCallService()
    {
        Assert.IsType<ForbidResult>(await _controller.Page(_testUserId + 1, DateTime.MinValue, DateTime.MaxValue, cancellationToken: TestContext.Current.CancellationToken));
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, false, false, false, false)]
    [InlineData(101, false, false, false, false)]
    [InlineData(25, true, false, false, false)]
    [InlineData(25, false, true, false, false)]
    [InlineData(25, false, false, true, false)]
    [InlineData(25, false, false, false, true)]
    public async Task Page_InvalidQuery_ReturnsValidationProblem(int size, bool reversed, bool date, bool type, bool id)
    {
        var day = new DateTime(2024, 3, 5);
        var result = await _controller.Page(_testUserId, day, reversed ? day.AddDays(-1) : day, size,
            date ? day : null, type ? AccountType.Currency : null, id ? 1 : null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }
}