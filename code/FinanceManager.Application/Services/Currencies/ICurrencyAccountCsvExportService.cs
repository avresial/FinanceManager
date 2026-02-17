using UserId = int;
using AccountId = int;

namespace FinanceManager.Application.Services.Currencies;

public interface ICurrencyAccountCsvExportService
{
    Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end);
}