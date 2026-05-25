using FinanceManager.Domain.Dtos;

namespace FinanceManager.Api.Services;

public interface ICurrencyImportJobChannel
{
    ValueTask QueueJob(CurrencyImportJobRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<CurrencyImportJobRequest> ReadAll(CancellationToken cancellationToken);
}

public sealed record CurrencyImportJobRequest(Guid JobId, int UserId, int AccountId, IReadOnlyList<CurrencyEntryImportRecordDto> Entries);