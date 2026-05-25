using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.Application.Services.Bonds;

public interface IBondEntryProvider
{
    Task<EntryRangeResult<BondAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0);
}