using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using System.Runtime.CompilerServices;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Bond.Export;

public class BondAccountExportService(
    IAccountRepository<BondAccount> bondAccountRepository,
    IBondAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    IBondDetailsRepository bondDetailsRepository) : IBondAccountExportService
{
    public async IAsyncEnumerable<BondAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var account = await bondAccountRepository.Get(accountId);
        if (account is null || account.UserId != userId)
            throw new InvalidOperationException("Account not found or access denied.");

        // Pre-load bond names once instead of querying per entry.
        var bondNamesById = new Dictionary<int, string>();
        await foreach (var bond in bondDetailsRepository.GetAllAsync(cancellationToken))
            bondNamesById[bond.Id] = bond.Name;

        await foreach (var entry in bondAccountEntryRepository.Get(accountId, start, end)
            .OrderBy(x => x.PostingDate)
            .ThenBy(x => x.EntryId)
            .WithCancellation(cancellationToken))
        {
            var bondName = bondNamesById.TryGetValue(entry.BondDetailsId, out var name)
                ? name
                : $"Bond #{entry.BondDetailsId}";

            yield return new BondAccountExportDto(entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange, bondName);
        }
    }
}