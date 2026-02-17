using UserId = int;
using AccountId = int;

namespace FinanceManager.Application.Services.Bonds;

public interface IBondAccountCsvExportService
{
    Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}