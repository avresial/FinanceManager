using FinanceManager.Domain.Entities.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.Services.Currencies;

public interface ICurrencyAccountExportService
{
    IAsyncEnumerable<CurrencyAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}