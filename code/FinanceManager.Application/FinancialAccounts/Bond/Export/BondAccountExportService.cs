using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using System.Runtime.CompilerServices;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Bond.Export;

public class BondAccountExportService(
    IAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    IBondDetailsRepository bondDetailsRepository) : IBondAccountExportService
{
    public async IAsyncEnumerable<BondAccountExportDto> GetOwnerExportResults(UserId userId, int bondDetailsId, DateTime start, DateTime end, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // One definition per export, so one lookup resolves the name, with the same fallback as an account export.
        var names = await bondDetailsRepository.GetNamesByIdsAsync([bondDetailsId], cancellationToken);
        var bondName = names.TryGetValue(bondDetailsId, out var name) ? name : $"Bond #{bondDetailsId}";

        // The owner stream is already in ascending date and id order. The running value is not part of an owner export.
        await foreach (var entry in bondAccountEntryRepository.GetByOwner(userId, bondDetailsId, start, end, cancellationToken))
            yield return new BondAccountExportDto(entry.EntryId, entry.PostingDate, entry.ValueChange, bondName, Description: entry.Description);
    }
}