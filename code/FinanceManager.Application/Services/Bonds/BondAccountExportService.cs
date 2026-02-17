using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Repositories.Account;
using System.Runtime.CompilerServices;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.Services.Bonds;

public class BondAccountExportService(IAccountRepository<BondAccount> bondAccountRepository,
    IBondAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository) : IBondAccountExportService
{
    public async IAsyncEnumerable<BondAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");

        await foreach (var entry in bondAccountEntryRepository.Get(accountId, start, end)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .WithCancellation(cancellationToken))
        {
            yield return new BondAccountExportDto(entry.PostingDate, entry.ValueChange, entry.BondDetailsId);
        }
    }
}