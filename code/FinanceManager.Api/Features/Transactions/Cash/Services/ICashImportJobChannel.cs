using FinanceManager.Domain.Transactions.Cash.Dtos;

namespace FinanceManager.Api.Features.Transactions.Cash.Services;

public interface ICashImportJobChannel
{
    ValueTask QueueJob(CashImportJobRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<CashImportJobRequest> ReadAll(CancellationToken cancellationToken);
}

public sealed record CashImportJobRequest(
    Guid JobId, int UserId, int CurrencyId, IReadOnlyList<CurrencyEntryImportRecordDto> Entries, int? GuestUserId = null);