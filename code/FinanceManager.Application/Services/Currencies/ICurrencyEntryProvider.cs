using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Application.Services.Currencies;

public interface ICurrencyEntryProvider
{
    Task<EntryRangeResult<CurrencyAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0);
}