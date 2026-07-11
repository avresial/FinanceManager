using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public interface ICurrencyAccountExportService
{
    IAsyncEnumerable<CurrencyAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}