using CsvHelper;
using System.Globalization;
using UserId = int;
using AccountId = int;

namespace FinanceManager.Application.Services.Currencies;

public class CurrencyAccountCsvExportService(ICurrencyAccountExportService currencyAccountExportService) : ICurrencyAccountCsvExportService
{
    public async Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        var exportItems = currencyAccountExportService.GetExportResults(userId, accountId, start, end);
        await csv.WriteRecordsAsync(exportItems);

        return writer.ToString();
    }
}