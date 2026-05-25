using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Application.Services.Stocks;

public interface IStockEntryProvider
{
    Task<EntryRangeResult<StockAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0);
}