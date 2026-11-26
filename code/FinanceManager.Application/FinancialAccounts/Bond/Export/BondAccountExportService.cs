using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
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

            yield return new BondAccountExportDto(entry.EntryId, entry.PostingDate, entry.Value, entry.ValueChange, bondName, Description: entry.Description);
        }
    }

    public async IAsyncEnumerable<BondAccountExportDto> GetOwnerExportResults(UserId userId, int bondDetailsId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // One definition per export, so one lookup resolves the name, with the same fallback as an account export.
        var names = await bondDetailsRepository.GetNamesByIdsAsync([bondDetailsId], cancellationToken);
        var bondName = names.TryGetValue(bondDetailsId, out var name) ? name : $"Bond #{bondDetailsId}";

        // The owner stream is already in ascending date and id order. The running value is not part of an owner export.
        await foreach (var entry in bondAccountEntryRepository.GetByOwner(userId, bondDetailsId, start, end, cancellationToken))
            yield return new BondAccountExportDto(entry.EntryId, entry.PostingDate, 0m, entry.ValueChange, bondName, Description: entry.Description);
    }
}