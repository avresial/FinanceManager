using UserId = int;
using AccountId = int;

namespace FinanceManager.Application.Services.Stocks;

public interface IStockAccountCsvExportService
{
    Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}