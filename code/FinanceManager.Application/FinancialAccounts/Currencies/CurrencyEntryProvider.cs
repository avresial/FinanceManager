using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.FinancialAccounts.Currencies;

public class CurrencyEntryProvider(IAccountEntryRepository<CurrencyAccountEntry> accountEntryRepository) : ICurrencyEntryProvider
{
    public async Task<EntryRangeResult<CurrencyAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumEntryCount);

        // Two queries, no loop: (1) cheaply project just the posting dates to decide how far back the
        // window must reach to include at least `minimumEntryCount` entries, then (2) load the entries
        // for that window once. This avoids repeatedly re-fetching a growing range from the database.
        var effectiveStartDate = await ResolveEffectiveStartDateAsync(accountId, startDate, endDate, minimumEntryCount);
        var entries = await LoadEntriesAsync(accountId, effectiveStartDate, endDate);

        return new(entries, effectiveStartDate);
    }

    private async Task<DateTime> ResolveEffectiveStartDateAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount)
    {
        if (minimumEntryCount == 0) return startDate;

        var candidateDates = (await accountEntryRepository.GetPostingDates(accountId))
            .Where(date => date <= endDate)
            .OrderByDescending(date => date)
            .ToList();

        // Fewer entries exist (up to endDate) than requested: take everything available.
        if (candidateDates.Count <= minimumEntryCount)
            return candidateDates.Count != 0 && candidateDates[^1] < startDate ? candidateDates[^1] : startDate;

        // The date of the Nth-newest entry is the furthest back we must reach to include at least
        // `minimumEntryCount` entries. If it already falls inside the requested range, keep startDate.
        var nthNewestDate = candidateDates[minimumEntryCount - 1];
        return nthNewestDate < startDate ? nthNewestDate : startDate;
    }

    private async Task<List<CurrencyAccountEntry>> LoadEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        List<CurrencyAccountEntry> entries = [];
        await foreach (var entry in accountEntryRepository.Get(accountId, startDate, endDate))
            entries.Add(entry);

        return entries;
    }
}