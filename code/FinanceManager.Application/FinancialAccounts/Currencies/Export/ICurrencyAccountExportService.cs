using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public interface ICurrencyAccountExportService
{
    IAsyncEnumerable<CurrencyAccountExportDto> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default);

    /// <summary>The owner's entries of one currency between two timestamps (inclusive), linked or not, oldest first.</summary>
    IAsyncEnumerable<CurrencyAccountExportDto> GetOwnerExportResults(UserId userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}