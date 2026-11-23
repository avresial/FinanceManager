using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Shared.Exports;

public interface IAccountCsvExportService<T> where T : IAccountExportDto
{
    Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>
    /// CSV of the owner's entries of one currency or bond definition, linked or not, oldest first. The account running
    /// value column is left out: a linked row's value and an unlinked row's placeholder mean different things.
    /// </summary>
    Task<string> GetOwnerExportResults(UserId userId, int partitionId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}