using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using System.Runtime.CompilerServices;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public class CurrencyAccountExportService(IAccountEntryRepository<CurrencyAccountEntry> currencyAccountEntryRepository) : ICurrencyAccountExportService
{
    public async IAsyncEnumerable<CurrencyAccountExportDto> GetOwnerExportResults(UserId userId, int currencyId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // The owner stream is already in ascending date and id order.
        await foreach (var entry in currencyAccountEntryRepository.GetByOwner(userId, currencyId, start, end, cancellationToken))
            yield return CurrencyAccountExportDto.FromEntity(entry);
    }
}