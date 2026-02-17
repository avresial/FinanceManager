using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Repositories.Account;
using UserId = int;
using AccountId = int;

namespace FinanceManager.Application.Services.Currencies;

public class CurrencyAccountExportService(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyAccountEntryRepository) : ICurrencyAccountExportService
{
    public async IAsyncEnumerable<CurrencyAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end)
    {
        var account = await currencyAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");

        var entries = currencyAccountEntryRepository.Get(accountId, start, end)
            .OrderBy(x => x.PostingDate)
            .ToListAsync();

        foreach (var entry in await entries)
        {
            yield return new CurrencyAccountExportDto(entry.PostingDate, entry.ValueChange, entry.ContractorDetails, entry.Description);
        }
    }
}