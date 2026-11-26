using FinanceManager.Domain.Transactions.Bonds.Exports;
using UserId = int;

namespace FinanceManager.Application.Transactions.Export;

public interface IBondExportService
{
    /// <summary>The owner's entries of one bond definition between two timestamps (inclusive), linked or not, oldest first.</summary>
    IAsyncEnumerable<BondExportDto> GetOwnerExportResults(UserId userId, int bondDetailsId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}