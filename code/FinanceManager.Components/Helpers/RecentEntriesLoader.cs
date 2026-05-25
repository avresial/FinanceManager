namespace FinanceManager.Components.Helpers;

public sealed record RecentEntriesLoaderOptions<TAccount>
{
    public required Func<DateTime, DateTime, Task<TAccount?>> LoadByDateRangeAsync { get; init; }
    public required Func<TAccount, int> GetEntryCount { get; init; }
    public required Func<TAccount, DateTime?> GetNextOlderReferenceDate { get; init; }
    public required Func<TAccount, bool> HasOlderEntries { get; init; }
}

public sealed record RecentEntriesLoadResult<TAccount>(TAccount? Account, DateTime DateStart);

public static class RecentEntriesLoader
{
    public static async Task<RecentEntriesLoadResult<TAccount>> LoadMoreAsync<TAccount>(
        TAccount currentAccount,
        DateTime dateStart,
        DateTime dateEnd,
        RecentEntriesLoaderOptions<TAccount> options)
    {
        var requestedDateStart = dateStart.AddMonths(-1);
        var entriesCountBeforeUpdate = options.GetEntryCount(currentAccount);
        var dateRangeAccount = await options.LoadByDateRangeAsync(requestedDateStart, dateEnd);
        if (dateRangeAccount is null)
            return new(currentAccount, dateStart);

        var account = dateRangeAccount;

        if (options.GetEntryCount(account) != entriesCountBeforeUpdate || !options.HasOlderEntries(account))
            return new(account, requestedDateStart);

        var nextOlderReferenceDate = options.GetNextOlderReferenceDate(account);
        if (nextOlderReferenceDate is null)
            return new(account, requestedDateStart);

        requestedDateStart = nextOlderReferenceDate.Value;
        account = await options.LoadByDateRangeAsync(requestedDateStart, dateEnd) ?? account;

        return new(account, requestedDateStart);
    }
}