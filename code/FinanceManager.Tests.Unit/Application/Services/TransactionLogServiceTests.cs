using FinanceManager.Application.Dashboard;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Services;

[Collection("Application")]
[Trait("Category", "Unit")]
public class TransactionLogServiceTests
{
    private const int _guestId = -7;
    private static readonly DateTime _day = new(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc);
    private readonly Mock<ICashTransactionReader> _cash = new(MockBehavior.Strict);
    private readonly Mock<IBondTransactionReader> _bonds = new(MockBehavior.Strict);
    private readonly Mock<IInvestmentTransactionRepository> _trades = new(MockBehavior.Strict);

    private TransactionLogService Service => new(_cash.Object, _bonds.Object, _trades.Object);

    [Fact]
    public async Task CompactFeedReadsSequentiallyAndReturnsSignedAmountsWithFallbackDescriptions()
    {
        var ct = TestContext.Current.CancellationToken;
        var sequence = new MockSequence();
        _cash.InSequence(sequence).Setup(r => r.GetHistory(_guestId, It.Is<OwnerHistoryQuery>(q => q.Take == 3 && !q.IncludeDetails), ct))
            .ReturnsAsync([new(null, 1, _day, -11m, "", 0, "PLN", "Bakery", [])]);
        _bonds.InSequence(sequence).Setup(r => r.GetHistory(_guestId, It.Is<OwnerHistoryQuery>(q => q.Take == 3 && !q.IncludeDetails), ct))
            .ReturnsAsync([new(null, 1, _day, 2m, "", 1, "PLN", "Treasury", [])]);
        _trades.InSequence(sequence).Setup(r => r.GetHistoryByUser(_guestId, It.Is<OwnerHistoryQuery>(q => q.Take == 3 && !q.IncludeDetails), ct))
            .ReturnsAsync([new InvestmentTransaction(1) { Type = InvestmentTransactionType.Sell, Quantity = 3, UnitPrice = 5,
                Currency = "GBX", PostingDate = _day, AssetListing = new() { Ticker = "ABC" } }]);
        var rows = await Service.GetLastTransactions(_guestId, 3, ct);
        Assert.Equal([
            new(TransactionKind.Cash, 1, _day, -11, "Bakery", "PLN"),
            new(TransactionKind.Bond, 1, _day, 2, "Treasury", "units"),
            new(TransactionKind.Investment, 1, _day, -15, "Sell 3 ABC", "GBX")], rows);
    }

    [Fact]
    public async Task MaxIdTieKeepsLaterKindsAndRequestsOneLookahead()
    {
        var ct = TestContext.Current.CancellationToken;
        var cursor = new TransactionLogCursor(_day, TransactionKind.Cash, long.MaxValue);
        _cash.Setup(r => r.GetHistory(_guestId, It.Is<OwnerHistoryQuery>(q => q.Boundary == new OwnerHistoryBoundary(_day, long.MaxValue, false)
            && q.Start == _day && q.End == _day && q.Take == 2 && q.IncludeDetails), ct)).ReturnsAsync([]);
        _bonds.Setup(r => r.GetHistory(_guestId, It.Is<OwnerHistoryQuery>(q => q.Boundary == new OwnerHistoryBoundary(_day, long.MaxValue, true)
            && q.Currencies!.Contains("PLN") && q.Search == "sale"), ct)).ReturnsAsync([
                new(null, long.MaxValue, _day, 1, "sale", 1, "PLN", null, []), new(9, 2, _day, 1, "sale", 1, "PLN", null, [])]);
        var page = await Service.GetItemsPage(_guestId, new(_day, _day.AddDays(1), 1, cursor,
            [TransactionKind.Cash, TransactionKind.Bond], ["PLN"], "sale"), ct);
        Assert.Null(Assert.Single(page.Items).LegacyAccountId);
        Assert.Equal(new(_day, TransactionKind.Bond, long.MaxValue), page.NextCursor);
        _trades.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IntradayCursorPreservesEarlierMidnightTradesAndNormalizesUnspecifiedDates()
    {
        var ct = TestContext.Current.CancellationToken;
        var noon = DateTime.SpecifyKind(_day.AddHours(12), DateTimeKind.Unspecified);
        _trades.Setup(r => r.GetHistoryByUser(_guestId, It.Is<OwnerHistoryQuery>(q =>
            q.Boundary!.Date == noon && q.Boundary.Date.Kind == DateTimeKind.Utc && q.End == noon && q.Take == 26), ct))
            .ReturnsAsync([new InvestmentTransaction(long.MaxValue) { PostingDate = _day }]);
        var page = await Service.GetItemsPage(_guestId, new(_day, _day.AddDays(1), Cursor: new(noon, TransactionKind.Cash, 1),
            Kinds: [TransactionKind.Investment]), ct);
        Assert.IsType<InvestmentTransactionLogItem>(Assert.Single(page.Items));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task CursorBeforeRangeSkipsAllReaders()
    {
        var page = await Service.GetItemsPage(_guestId, new(_day, _day, Cursor: new(_day.AddTicks(-1), TransactionKind.Cash, 1)), TestContext.Current.CancellationToken);
        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task InvalidLimitsRejected(int count)
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Service.GetLastTransactions(_guestId, count, ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Service.GetItemsPage(_guestId, new(_day, _day, count), ct));
    }

    [Fact]
    public async Task CancelledAndInvalidQueriesDoNotOpenReaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service.GetItemsPage(_guestId, new(_day, _day), new CancellationToken(true)));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetItemsPage(_guestId, new(_day, _day.AddTicks(-1)), ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.GetItemsPage(_guestId, new(_day, _day, Cursor: new(_day, TransactionKind.Cash, 0)), ct));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Service.GetItemsPage(0, new(_day, _day), ct));
    }
}