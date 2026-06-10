using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;

namespace FinanceManager.Application.FinancialAccounts.Currencies;

public interface ICurrencyEntryProvider
{
    Task<EntryRangeResult<CurrencyAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0);
}