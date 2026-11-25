using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public interface ICurrencyAccountExportService
{
    /// <summary>The owner's entries of one currency between two timestamps (inclusive), linked or not, oldest first.</summary>
    IAsyncEnumerable<CurrencyAccountExportDto> GetOwnerExportResults(UserId userId, int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}