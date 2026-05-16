using FinanceManager.Components.Helpers;

namespace FinanceManager.UnitTests.Helpers;

[Collection("Api")]
[Trait("Category", "Unit")]
public class RecentEntriesLoaderTests
{
    [Fact]
    public async Task LoadMoreAsync_ReturnsExpandedMonth_WhenPreviousMonthAddsEntries()
    {
        var initialDateStart = new DateTime(2026, 4, 1);
        var dateEnd = new DateTime(2026, 4, 26);
        var previousMonthDateStart = initialDateStart.AddMonths(-1);
        var currentAccount = new TestAccount(
        [
            new(1, new DateTime(2026, 4, 20)),
            new(2, new DateTime(2026, 4, 12)),
        ],
        [
            new(3, new DateTime(2026, 3, 1)),
        ]);
        var expandedAccount = new TestAccount(
        [
            new(1, new DateTime(2026, 4, 20)),
            new(2, new DateTime(2026, 4, 12)),
            new(3, new DateTime(2026, 3, 1)),
        ], []);

        var result = await RecentEntriesLoader.LoadMoreAsync(
            currentAccount,
            initialDateStart,
            dateEnd,
            CreateOptions(
                loadByDateRangeAsync: (start, end) =>
                {
                    Assert.Equal(previousMonthDateStart, start);
                    Assert.Equal(dateEnd, end);
                    return Task.FromResult<TestAccount?>(expandedAccount);
                }));

        Assert.Equal(expandedAccount, result.Account);
        Assert.Equal(previousMonthDateStart, result.DateStart);
    }

    [Fact]
    public async Task LoadMoreAsync_UsesNextOlderReferenceDateWhenPreviousMonthAddsNoEntries()
    {
        var initialDateStart = new DateTime(2026, 4, 1);
        var dateEnd = new DateTime(2026, 4, 26);
        var firstAttemptDateStart = initialDateStart.AddMonths(-1);
        var fallbackDateStart = new DateTime(2026, 2, 10);

        var currentAccount = new TestAccount(
        [
            new(1, new DateTime(2026, 4, 20)),
            new(2, new DateTime(2026, 4, 12)),
        ], []);
        var unchangedAccount = new TestAccount(
        [
            new(1, new DateTime(2026, 4, 20)),
            new(2, new DateTime(2026, 4, 12)),
        ],
        [
            new(3, fallbackDateStart),
            new(4, new DateTime(2026, 1, 5)),
        ]);
        var expandedAccount = new TestAccount(
        [
            new(1, new DateTime(2026, 4, 20)),
            new(2, new DateTime(2026, 4, 12)),
            new(3, fallbackDateStart),
        ],
        [
            new(4, new DateTime(2026, 1, 5)),
        ]);

        var requestedStarts = new List<DateTime>();

        var result = await RecentEntriesLoader.LoadMoreAsync(
            currentAccount,
            initialDateStart,
            dateEnd,
            CreateOptions(
                loadByDateRangeAsync: (start, end) =>
                {
                    requestedStarts.Add(start);
                    Assert.Equal(dateEnd, end);
                    if (start == firstAttemptDateStart)
                        return Task.FromResult<TestAccount?>(unchangedAccount);
                    if (start == fallbackDateStart)
                        return Task.FromResult<TestAccount?>(expandedAccount);

                    return Task.FromResult<TestAccount?>(null);
                }));

        Assert.Equal([firstAttemptDateStart, fallbackDateStart], requestedStarts);
        Assert.Equal(fallbackDateStart, result.DateStart);
        Assert.Equal(expandedAccount, result.Account);
    }

    [Fact]
    public async Task LoadMoreAsync_KeepsCurrentStateWhenRangeRequestReturnsNull()
    {
        var initialDateStart = new DateTime(2026, 4, 1);
        var dateEnd = new DateTime(2026, 4, 26);
        var currentAccount = new TestAccount(
        [
            new(1, new DateTime(2026, 4, 20)),
            new(2, new DateTime(2026, 4, 12)),
        ], []);

        var result = await RecentEntriesLoader.LoadMoreAsync(
            currentAccount,
            initialDateStart,
            dateEnd,
            CreateOptions(
                loadByDateRangeAsync: (_, _) => Task.FromResult<TestAccount?>(null)));

        Assert.Equal(currentAccount, result.Account);
        Assert.Equal(initialDateStart, result.DateStart);
    }

    private static RecentEntriesLoaderOptions<TestAccount> CreateOptions(
        Func<DateTime, DateTime, Task<TestAccount?>> loadByDateRangeAsync) =>
        new()
        {
            LoadByDateRangeAsync = loadByDateRangeAsync,
            GetEntryCount = account => account.Entries.Count,
            GetNextOlderReferenceDate = account => account.NextOlderEntries.Select(x => (DateTime?)x.PostingDate).Max(),
            HasOlderEntries = account => account.NextOlderEntries.Count != 0,
        };

    private sealed record TestAccount(IReadOnlyList<TestEntry> Entries, IReadOnlyList<TestEntry> NextOlderEntries);

    private sealed record TestEntry(int EntryId, DateTime PostingDate);
}
