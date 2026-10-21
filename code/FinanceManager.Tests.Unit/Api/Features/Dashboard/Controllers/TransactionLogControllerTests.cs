using FinanceManager.Api.Features.Dashboard.Controllers;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
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