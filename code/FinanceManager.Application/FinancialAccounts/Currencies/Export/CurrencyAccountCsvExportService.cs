using CsvHelper;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Currencies.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using System.Globalization;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Currencies.Export;

public class CurrencyAccountCsvExportService(ICurrencyAccountExportService currencyAccountExportService) : IAccountCsvExportService<CurrencyAccountExportDto>
{
    public async Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<CurrencyAccountExportDtoMap>();
        csv.WriteHeader<CurrencyAccountExportDto>();
        await csv.NextRecordAsync();

        var exportItems = currencyAccountExportService.GetExportResults(userId, accountId, start, end, cancellationToken);
        await foreach (var exportItem in exportItems.WithCancellation(cancellationToken))
        {
            csv.WriteRecord(exportItem);
            await csv.NextRecordAsync();
        }

        return writer.ToString();
    }

    public string GetExportResults(IReadOnlyList<CurrencyAccountExportDto> entries)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<CurrencyAccountExportDtoMap>();
        csv.WriteHeader<CurrencyAccountExportDto>();
        csv.NextRecord();
        csv.WriteRecords(entries);
        return writer.ToString();
    }
}