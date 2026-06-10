using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.Application.FinancialAccounts.Bond;

public interface IBondEntryProvider
{
    Task<EntryRangeResult<BondAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0);
}