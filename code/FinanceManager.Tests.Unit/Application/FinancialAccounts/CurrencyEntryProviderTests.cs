using FinanceManager.Application.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories.Account;
using Moq;

namespace FinanceManager.Tests.Unit.Application.FinancialAccounts;

[Trait("Category", "Unit")]
public class CurrencyEntryProviderTests
{
    private const int _accountId = 1;
    private static readonly DateTime _startDate = new(2026, 4, 1);
    private static readonly DateTime _endDate = new(2026, 4, 30);

    private readonly Mock<IAccountEntryRepository<CurrencyAccountEntry>> _repository = new();
    private readonly CurrencyEntryProvider _provider;

    public CurrencyEntryProviderTests() => _provider = new CurrencyEntryProvider(_repository.Object);

    [Fact]
    public async Task GetEntriesAsync_WithZeroMinimum_LoadsRangeOnce_WithoutProbingPostingDates()
    {
        var entries = Entries((2, new DateTime(2026, 4, 20)), (1, new DateTime(2026, 4, 5)));
        _repository.Setup(r => r.Get(_accountId, _startDate, _endDate)).Returns(entries.ToAsyncEnumerable());

        var result = await _provider.GetEntriesAsync(_accountId, _startDate, _endDate, minimumEntryCount: 0);

        Assert.Equal(_startDate, result.EffectiveStartDate);
        Assert.Equal([2, 1], result.Entries.Select(e => e.EntryId));
        _repository.Verify(r => r.GetPostingDates(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetEntriesAsync_WhenRangeAlreadyMeetsMinimum_KeepsRequestedStartDate()
    {
        _repository.Setup(r => r.GetPostingDates(_accountId))
            .ReturnsAsync([new DateTime(2026, 4, 20), new DateTime(2026, 4, 10), new DateTime(2026, 4, 5)]);
        var entries = Entries((3, new DateTime(2026, 4, 20)), (2, new DateTime(2026, 4, 10)), (1, new DateTime(2026, 4, 5)));
        _repository.Setup(r => r.Get(_accountId, _startDate, _endDate)).Returns(entries.ToAsyncEnumerable());

        var result = await _provider.GetEntriesAsync(_accountId, _startDate, _endDate, minimumEntryCount: 2);

        Assert.Equal(_startDate, result.EffectiveStartDate);
        Assert.Equal([3, 2, 1], result.Entries.Select(e => e.EntryId));
    }

    [Fact]
    public async Task GetEntriesAsync_WhenRangeTooSparse_ExpandsStartToNthNewestDate_InTwoQueries()
    {
        var expandedStart = new DateTime(2026, 3, 15);
        _repository.Setup(r => r.GetPostingDates(_accountId))
            .ReturnsAsync([new DateTime(2026, 4, 20), new DateTime(2026, 4, 10), expandedStart, new DateTime(2026, 2, 1)]);
        var entries = Entries((4, new DateTime(2026, 4, 20)), (3, new DateTime(2026, 4, 10)), (2, expandedStart));
        _repository.Setup(r => r.Get(_accountId, expandedStart, _endDate)).Returns(entries.ToAsyncEnumerable());

        var result = await _provider.GetEntriesAsync(_accountId, _startDate, _endDate, minimumEntryCount: 3);

        Assert.Equal(expandedStart, result.EffectiveStartDate);
        Assert.Equal([4, 3, 2], result.Entries.Select(e => e.EntryId));
        // Exactly two queries, no expansion loop.
        _repository.Verify(r => r.GetPostingDates(_accountId), Times.Once);
        _repository.Verify(r => r.Get(_accountId, It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task GetEntriesAsync_WhenFewerEntriesExistThanMinimum_UsesOldestAvailableDate()
    {
        var oldest = new DateTime(2026, 2, 1);
        _repository.Setup(r => r.GetPostingDates(_accountId))
            .ReturnsAsync([new DateTime(2026, 4, 20), new DateTime(2026, 3, 15), oldest]);
        var entries = Entries((3, new DateTime(2026, 4, 20)), (2, new DateTime(2026, 3, 15)), (1, oldest));
        _repository.Setup(r => r.Get(_accountId, oldest, _endDate)).Returns(entries.ToAsyncEnumerable());

        var result = await _provider.GetEntriesAsync(_accountId, _startDate, _endDate, minimumEntryCount: 10);

        Assert.Equal(oldest, result.EffectiveStartDate);
        Assert.Equal([3, 2, 1], result.Entries.Select(e => e.EntryId));
    }

    [Fact]
    public async Task GetEntriesAsync_WithNegativeMinimum_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _provider.GetEntriesAsync(_accountId, _startDate, _endDate, minimumEntryCount: -1));
    }

    private static List<CurrencyAccountEntry> Entries(params (int EntryId, DateTime PostingDate)[] items) =>
        [.. items.Select(i => new CurrencyAccountEntry(_accountId, i.EntryId, i.PostingDate, 0m, 0m))];
}