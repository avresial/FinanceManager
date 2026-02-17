using FinanceManager.Domain.Entities.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.Services.Bonds;

public interface IBondAccountExportService
{
    IAsyncEnumerable<BondAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}