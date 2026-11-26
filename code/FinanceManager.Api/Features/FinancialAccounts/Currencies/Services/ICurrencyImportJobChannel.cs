using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;

public interface ICurrencyImportJobChannel
{
    ValueTask QueueJob(CurrencyImportJobRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<CurrencyImportJobRequest> ReadAll(CancellationToken cancellationToken);
}

/// <summary>A queued cash import. A null <paramref name="AccountId"/> imports into the owner's pool of <paramref name="CurrencyId"/>.</summary>
public sealed record CurrencyImportJobRequest(
    Guid JobId, int UserId, int? AccountId, IReadOnlyList<CurrencyEntryImportRecordDto> Entries, int? GuestUserId = null, int? CurrencyId = null);