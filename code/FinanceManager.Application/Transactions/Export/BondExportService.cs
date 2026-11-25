using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Bonds.Exports;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Shared.Repositories;
using System.Runtime.CompilerServices;
using UserId = int;

namespace FinanceManager.Application.Transactions.Export;

public class BondExportService(
    ITransactionEntryRepository<BondEntry> bondAccountEntryRepository,
    IBondDetailsRepository bondDetailsRepository) : IBondExportService
{
    public async IAsyncEnumerable<BondExportDto> GetOwnerExportResults(UserId userId, int bondDetailsId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // One definition per export, so one lookup resolves the name, with the same fallback as an account export.
        var names = await bondDetailsRepository.GetNamesByIdsAsync([bondDetailsId], cancellationToken);
        var bondName = names.TryGetValue(bondDetailsId, out var name) ? name : $"Bond #{bondDetailsId}";

        // The owner stream is already in ascending date and id order. The running value is not part of an owner export.
        await foreach (var entry in bondAccountEntryRepository.GetByOwner(userId, bondDetailsId, start, end, cancellationToken))
            yield return new BondExportDto(entry.EntryId, entry.PostingDate, entry.ValueChange, bondName, Description: entry.Description);
    }
}