using FinanceManager.Application.Insights.Generation;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Transactions.Bonds.Dtos;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using FinanceManager.Tests.Unit.Shared.Time;
using Moq;

namespace FinanceManager.Tests.Unit.Application.Insights.Generation;

[Collection("Application")]
[Trait("Category", "Unit")]
public class InsightsContextBuilderTests
{
    private static readonly DateTime _now = new(2026, 9, 11, 14, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ICashTransactionReader> _cash = new();
    private readonly Mock<IBondTransactionReader> _bonds = new();

    [Fact]
    public async Task Build_ReadsTheOwnersLastThreeMonthsWithBoundedRowsAndNoDetails()
    {
        OwnerHistoryQuery? cashQuery = null;
        OwnerHistoryQuery? bondQuery = null;
        _cash.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<int, OwnerHistoryQuery, CancellationToken>((_, q, _) => cashQuery = q)
            .ReturnsAsync([]);
        _bonds.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<int, OwnerHistoryQuery, CancellationToken>((_, q, _) => bondQuery = q)
            .ReturnsAsync([]);

        await Builder().BuildEntriesContextCsv(7, TestContext.Current.CancellationToken);

        Assert.Equal(new DateTime(2026, 6, 11, 14, 0, 0, DateTimeKind.Utc), cashQuery!.Start);
        Assert.Equal(_now, cashQuery.End);
        Assert.Equal(250, cashQuery.Take);
        Assert.False(cashQuery.IncludeDetails);
        Assert.Equal(200, bondQuery!.Take);
        Assert.False(bondQuery.IncludeDetails);
    }

    [Fact]
    public async Task Build_EmitsOnlyCashAndBondSectionsWithoutLabelsOrTrades()
    {
        var label = new FinancialLabel { Id = 1, Name = "Secret label" };
        _cash.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CashLogEntry(1, new DateTime(2026, 9, 1), -12.5m, "Coffee, \"large\"", 0, "PLN", "Cafe", [label])]);
        _bonds.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new BondLogEntry(2, new DateTime(2026, 9, 2), 10m, "Buy", 3, "EUR", "EDO0834", [label])]);

        var csv = await Builder().BuildEntriesContextCsv(7, TestContext.Current.CancellationToken);

        Assert.Contains("[cash]", csv);
        Assert.Contains("PostingDate,Currency,ValueChange,ContractorDetails,Description", csv);
        Assert.Contains("2026-09-01,PLN,-12.5,Cafe,\"Coffee, \"\"large\"\"\"", csv);
        Assert.Contains("[bonds]", csv);
        Assert.Contains("PostingDate,BondName,Currency,ValueChange,Description", csv);
        Assert.Contains("2026-09-02,EDO0834,EUR,10,Buy", csv);
        Assert.DoesNotContain("[investments]", csv);
        Assert.DoesNotContain("[account]", csv);
        Assert.DoesNotContain("accountId", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret label", csv);
        Assert.DoesNotContain("Labels", csv);
    }

    [Fact]
    public async Task Build_OmitsEmptySections()
    {
        _cash.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CashLogEntry(1, new DateTime(2026, 9, 1), -1m, "x", 0, "PLN", null, [])]);
        _bonds.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var csv = await Builder().BuildEntriesContextCsv(7, TestContext.Current.CancellationToken);

        Assert.Contains("[cash]", csv);
        Assert.DoesNotContain("[bonds]", csv);
    }

    [Fact]
    public async Task Build_UserZero_ReturnsOnlyTheHeaderWithoutReading()
    {
        var csv = await Builder().BuildEntriesContextCsv(0, TestContext.Current.CancellationToken);

        Assert.Contains("timeRangeUtcStart,", csv);
        Assert.Contains("timeRangeUtcEnd,", csv);
        Assert.DoesNotContain("[cash]", csv);
        _cash.Verify(r => r.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        _bonds.Verify(r => r.GetHistory(It.IsAny<int>(), It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Build_NegativeGuestId_IsReadLikeAnyOwner()
    {
        _cash.Setup(r => r.GetHistory(-4, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CashLogEntry(1, new DateTime(2026, 9, 1), -1m, "demo", 0, "PLN", null, [])]);
        _bonds.Setup(r => r.GetHistory(-4, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var csv = await Builder().BuildEntriesContextCsv(-4, TestContext.Current.CancellationToken);

        Assert.Contains("[cash]", csv);
        Assert.Contains("demo", csv);
    }

    [Fact]
    public async Task Build_TruncatesEachSectionAtWholeRowsWithinTheCharacterBudget()
    {
        var description = new string('d', 1_000);
        var rows = Enumerable.Range(1, 250)
            .Select(id => new CashLogEntry(id, new DateTime(2026, 9, 1), -1m, description, 0, "PLN", null, []))
            .ToList();
        _cash.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        _bonds.Setup(r => r.GetHistory(7, It.IsAny<OwnerHistoryQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var csv = await Builder().BuildEntriesContextCsv(7, TestContext.Current.CancellationToken);

        var cashLines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(line => line.Contains(description)).ToList();
        Assert.InRange(cashLines.Count, 1, 43);
        Assert.All(cashLines, line => Assert.EndsWith(description, line.TrimEnd('\r')));
        Assert.True(csv.Length < 44_000 + 500);
    }

    private InsightsContextBuilder Builder() => new(_cash.Object, _bonds.Object, new FakeDateTimeProvider(_now));
}