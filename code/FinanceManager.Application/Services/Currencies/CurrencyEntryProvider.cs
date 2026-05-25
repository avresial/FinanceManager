using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services.Currencies;

public class CurrencyEntryProvider(IAccountEntryRepository<CurrencyAccountEntry> accountEntryRepository) : ICurrencyEntryProvider
{
    public async Task<EntryRangeResult<CurrencyAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumEntryCount);

        var effectiveStartDate = startDate;
        var entries = await LoadEntriesAsync(accountId, effectiveStartDate, endDate);

        while (minimumEntryCount > 0 && entries.Count < minimumEntryCount)
        {
            var nextOlderStartDate = await GetNextOlderStartDateAsync(accountId, entries, effectiveStartDate);
            if (nextOlderStartDate is null || nextOlderStartDate.Value >= effectiveStartDate)
                break;

            effectiveStartDate = nextOlderStartDate.Value;
            entries = await LoadEntriesAsync(accountId, effectiveStartDate, endDate);
        }

        return new(entries, effectiveStartDate);
    }

    private async Task<List<CurrencyAccountEntry>> LoadEntriesAsync(int accountId, DateTime startDate, DateTime endDate)
    {
        List<CurrencyAccountEntry> entries = [];
        await foreach (var entry in accountEntryRepository.Get(accountId, startDate, endDate))
            entries.Add(entry);

        return entries;
    }

    private async Task<DateTime?> GetNextOlderStartDateAsync(int accountId, IReadOnlyList<CurrencyAccountEntry> entries, DateTime effectiveStartDate)
    {
        var referenceDate = entries.Count != 0 ? entries.Min(x => x.PostingDate) : effectiveStartDate;
        return (await accountEntryRepository.GetNextOlder(accountId, referenceDate))?.PostingDate;
    }
}