using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Bond.Export;

public interface IBondAccountExportService
{
    IAsyncEnumerable<BondAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>The owner's entries of one bond definition between two timestamps (inclusive), linked or not, oldest first.</summary>
    IAsyncEnumerable<BondAccountExportDto> GetOwnerExportResults(UserId userId, int bondDetailsId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}