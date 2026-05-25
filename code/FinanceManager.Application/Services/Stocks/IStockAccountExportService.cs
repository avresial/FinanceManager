using FinanceManager.Domain.Entities.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.Services.Stocks;

public interface IStockAccountExportService
{
    IAsyncEnumerable<StockAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}