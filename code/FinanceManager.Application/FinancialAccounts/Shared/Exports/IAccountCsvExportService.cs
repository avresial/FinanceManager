using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Shared.Exports;

public interface IAccountCsvExportService<T> where T : IAccountExportDto
{
    Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    string GetExportResults(IReadOnlyList<T> entries);
}