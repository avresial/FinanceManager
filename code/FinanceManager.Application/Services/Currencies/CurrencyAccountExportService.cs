using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories.Account;
using System.Runtime.CompilerServices;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.Services.Currencies;

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
}