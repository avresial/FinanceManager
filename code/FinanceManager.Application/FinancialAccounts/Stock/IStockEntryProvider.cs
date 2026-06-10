using FinanceManager.Application.FinancialAccounts.Shared;
using FinanceManager.Domain.Entities.Stocks;

namespace FinanceManager.Application.FinancialAccounts.Stock;

public interface IStockEntryProvider
{
    Task<EntryRangeResult<StockAccountEntry>> GetEntriesAsync(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0);
}