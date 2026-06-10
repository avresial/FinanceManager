using FinanceManager.Domain.Entities.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Bond.Export;

public interface IBondAccountExportService
{
    IAsyncEnumerable<BondAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}