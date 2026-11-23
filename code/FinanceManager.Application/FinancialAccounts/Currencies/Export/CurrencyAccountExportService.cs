using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using System.Runtime.CompilerServices;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public class CurrencyAccountExportService(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyAccountEntryRepository) : ICurrencyAccountExportService
{
    public async IAsyncEnumerable<CurrencyAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var account = await currencyAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");

        await foreach (var entry in currencyAccountEntryRepository.Get(accountId, start, end)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .WithCancellation(cancellationToken))
        {
            yield return CurrencyAccountExportDto.FromEntity(entry);
        }
    }

    public async IAsyncEnumerable<CurrencyAccountExportDto> GetOwnerExportResults(UserId userId, int currencyId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // The owner stream is already in ascending date and id order.
        await foreach (var entry in currencyAccountEntryRepository.GetByOwner(userId, currencyId, start, end, cancellationToken))
            yield return CurrencyAccountExportDto.FromEntity(entry);
    }
}