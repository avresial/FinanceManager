using FinanceManager.Api.Features.Dashboard.Controllers;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Exists_ForTheOwner_ReturnsTheServiceAnswer(bool hasAny)
    {
        _transactionLogService.Setup(x => x.HasAny(_testUserId, TestContext.Current.CancellationToken)).ReturnsAsync(hasAny);

        var result = await _controller.Exists(_testUserId, TestContext.Current.CancellationToken);

        Assert.Equal(hasAny, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Exists_ForAnotherUser_IsForbiddenWithoutReading()
    {
        var result = await _controller.Exists(_testUserId + 1, TestContext.Current.CancellationToken);

        Assert.IsType<ForbidResult>(result);
        _transactionLogService.Verify(x => x.HasAny(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Page_PassesSearchAlongsideExistingFilters()
    {
        var day = new DateTime(2026, 10, 20);
        var query = new TransactionLogQuery(day, day, Kinds: [TransactionKind.Cash], Currencies: ["USD"], Search: "Café & bond");
        var page = new TransactionLogItemsPage([], null);
        _transactionLogService.Setup(x => x.GetItemsPage(_testUserId, It.Is<TransactionLogQuery>(q =>
            q.Search == query.Search && q.Kinds!.Contains(TransactionKind.Cash) && q.Currencies!.Contains("USD")),
            TestContext.Current.CancellationToken)).ReturnsAsync(page);
        var result = await _controller.ItemsPage(_testUserId, day, day, kinds: [TransactionKind.Cash], currencies: ["usd"],
            search: query.Search, cancellationToken: TestContext.Current.CancellationToken);
        Assert.IsType<TransactionLogItemsPageDto>(Assert.IsType<OkObjectResult>(result).Value);
        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task ItemsPage_MapsEachItemKindAndTheCursor_ToTheWireContract()
    {
        var day = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var cursor = new TransactionLogCursor(day, TransactionKind.Bond, 2);
        _transactionLogService.Setup(x => x.GetItemsPage(_testUserId, It.Is<TransactionLogQuery>(q => q.Search == "x" && q.PageSize == 3),
            TestContext.Current.CancellationToken)).ReturnsAsync(new TransactionLogItemsPage(
        [
            new CurrencyTransactionLogItem(new(1, day, -10, "Lunch", 3, "EUR", null, [])),
            new BondTransactionLogItem(new(2, day, 5, "Bond", 7, "USD", null, [])),
            new InvestmentTransactionLogItem(new InvestmentTransaction
            {
                EntryId = 3, AccountId = 30, TradeDate = DateOnly.FromDateTime(day), Quantity = 1, UnitPrice = 2, Currency = "USD"
            }),
        ], cursor));

        var result = await _controller.ItemsPage(_testUserId, day, day, pageSize: 3, search: "x",
            cancellationToken: TestContext.Current.CancellationToken);

        var page = Assert.IsType<TransactionLogItemsPageDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(new TransactionLogCursorDto(day, TransactionKindDto.Bond, 2), page.NextCursor);
        var cash = Assert.IsType<CurrencyTransactionLogItemDto>(page.Items[0]);
        Assert.Equal((3, "EUR", 1L, "Lunch"), (cash.CurrencyId, cash.Currency, cash.EntryId, cash.Description));
        Assert.Equal(7, Assert.IsType<BondTransactionLogItemDto>(page.Items[1]).BondDetailsId);
        Assert.Equal(3L, Assert.IsType<InvestmentTransactionLogItemDto>(page.Items[2]).Transaction.Id);
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
            new(TransactionKind.Cash, 1, new DateTime(2024, 3, 5), -50, "Groceries"),
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
        var cursor = new TransactionLogCursor(day, TransactionKind.Bond, 12);
        var query = new TransactionLogQuery(day.AddDays(-1), day, 2, cursor);
        var page = new TransactionLogItemsPage([], null);
        _transactionLogService.Setup(s => s.GetItemsPage(_testUserId, query, TestContext.Current.CancellationToken)).ReturnsAsync(page);

        var result = await _controller.ItemsPage(_testUserId, query.Start, query.End, 2,
            cursor.Date, cursor.Kind, cursor.EntryId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<TransactionLogItemsPageDto>(Assert.IsType<OkObjectResult>(result).Value);
        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task Page_WithFilters_PassesKindsAndCurrenciesToTheQuery()
    {
        var day = new DateTime(2024, 3, 5);
        var page = new TransactionLogItemsPage([], null);
        _transactionLogService.Setup(s => s.GetItemsPage(_testUserId, It.Is<TransactionLogQuery>(q =>
                q.Kinds!.SequenceEqual(new[] { TransactionKind.Bond, TransactionKind.Investment }) &&
                q.Currencies!.SequenceEqual(new[] { "USD", "EUR" })), TestContext.Current.CancellationToken))
            .ReturnsAsync(page);

        var result = await _controller.ItemsPage(_testUserId, day.AddDays(-1), day,
            kinds: [TransactionKind.Bond, TransactionKind.Investment], currencies: ["usd", " EUR ", "USD"],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<TransactionLogItemsPageDto>(Assert.IsType<OkObjectResult>(result).Value);
        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task Page_WithoutFilters_PassesNoFilterToTheQuery()
    {
        var day = new DateTime(2024, 3, 5);
        _transactionLogService.Setup(s => s.GetItemsPage(_testUserId, It.Is<TransactionLogQuery>(q =>
                q.Kinds == null && q.Currencies == null), TestContext.Current.CancellationToken))
            .ReturnsAsync(new TransactionLogItemsPage([], null));

        await _controller.ItemsPage(_testUserId, day.AddDays(-1), day, cancellationToken: TestContext.Current.CancellationToken);

        _transactionLogService.VerifyAll();
    }

    [Fact]
    public async Task Page_WithMaximumNumberOfCurrencies_IsAccepted()
    {
        var day = new DateTime(2024, 3, 5);
        _transactionLogService.Setup(s => s.GetItemsPage(_testUserId, It.IsAny<TransactionLogQuery>(), TestContext.Current.CancellationToken))
            .ReturnsAsync(new TransactionLogItemsPage([], null));

        var result = await _controller.ItemsPage(_testUserId, day.AddDays(-1), day,
            currencies: [.. Enumerable.Repeat("USD", 100)], cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Page_WithUndefinedAccountType_ReturnsValidationProblem()
    {
        var day = new DateTime(2024, 3, 5);

        var result = await _controller.ItemsPage(_testUserId, day.AddDays(-1), day,
            kinds: [TransactionKind.Cash, (TransactionKind)99], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Page_WithMalformedCurrencyCode_ReturnsValidationProblem(int code)
    {
        var day = new DateTime(2024, 3, 5);

        var result = await _controller.ItemsPage(_testUserId, day.AddDays(-1), day,
            currencies: ["USD", code.ToString()], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Page_WithTooManyCurrencies_ReturnsValidationProblem()
    {
        var day = new DateTime(2024, 3, 5);

        var result = await _controller.ItemsPage(_testUserId, day.AddDays(-1), day,
            currencies: [.. Enumerable.Repeat("USD", 101)], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Currencies_ReturnsTheOwnersCurrencies()
    {
        _transactionLogService.Setup(x => x.GetCurrencies(_testUserId, TestContext.Current.CancellationToken)).ReturnsAsync(["PLN", "USD"]);

        var result = await _controller.Currencies(_testUserId, TestContext.Current.CancellationToken);

        Assert.Equal(["PLN", "USD"], Assert.IsType<List<string>>(Assert.IsType<OkObjectResult>(result).Value));
    }

    [Fact]
    public async Task Currencies_ForOtherUser_DoesNotCallService()
    {
        Assert.IsType<ForbidResult>(await _controller.Currencies(_testUserId + 1, TestContext.Current.CancellationToken));
        _transactionLogService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Page_ForOtherUser_DoesNotCallService()
    {
        Assert.IsType<ForbidResult>(await _controller.ItemsPage(_testUserId + 1, DateTime.MinValue, DateTime.MaxValue, cancellationToken: TestContext.Current.CancellationToken));
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
        var result = await _controller.ItemsPage(_testUserId, day, reversed ? day.AddDays(-1) : day, size,
            date ? day : null, type ? TransactionKind.Cash : null, id ? 1 : null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        _transactionLogService.VerifyNoOtherCalls();
    }
}