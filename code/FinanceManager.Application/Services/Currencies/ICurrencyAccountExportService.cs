using FinanceManager.Domain.Entities.Exports;
using UserId = int;
using AccountId = int;

namespace FinanceManager.Application.Services.Currencies;

public interface ICurrencyAccountExportService
{
    IAsyncEnumerable<CurrencyAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end);
}