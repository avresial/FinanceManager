using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Cash.Exports;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using System.Runtime.CompilerServices;
using UserId = int;

namespace FinanceManager.Application.Transactions.Export;

public class CashExportService(ITransactionEntryRepository<CashEntry> currencyAccountEntryRepository) : ICashExportService
{
    public async IAsyncEnumerable<CashExportDto> GetOwnerExportResults(UserId userId, int currencyId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // The owner stream is already in ascending date and id order.
        await foreach (var entry in currencyAccountEntryRepository.GetByOwner(userId, currencyId, start, end, cancellationToken))
            yield return CashExportDto.FromEntity(entry);
    }
}