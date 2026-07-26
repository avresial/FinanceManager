using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;

namespace FinanceManager.Api.Features.FinancialAccounts.Currencies.Services;

public interface ICurrencyImportJobChannel
{
    ValueTask QueueJob(CurrencyImportJobRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<CurrencyImportJobRequest> ReadAll(CancellationToken cancellationToken);
}

public sealed record CurrencyImportJobRequest(Guid JobId, int UserId, int AccountId, IReadOnlyList<CurrencyEntryImportRecordDto> Entries);