using FinanceManager.Domain.Transactions.Cash.Exports;
using UserId = int;

namespace FinanceManager.Application.Transactions.Export;

public interface ICashExportService
{
    /// <summary>The owner's entries of one currency between two timestamps (inclusive), linked or not, oldest first.</summary>
    IAsyncEnumerable<CashExportDto> GetOwnerExportResults(UserId userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}