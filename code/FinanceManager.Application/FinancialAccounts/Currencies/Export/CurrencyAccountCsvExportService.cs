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
    public async Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        await WriteCsv(new CurrencyAccountExportDtoMap(), currencyAccountExportService.GetExportResults(userId, accountId, start, end, cancellationToken), cancellationToken);

    public async Task<string> GetOwnerExportResults(UserId userId, int partitionId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        await WriteCsv(new CurrencyAccountExportDtoMap(includeValue: false), currencyAccountExportService.GetOwnerExportResults(userId, partitionId, start, end, cancellationToken), cancellationToken);

    private static async Task<string> WriteCsv(CurrencyAccountExportDtoMap map, IAsyncEnumerable<CurrencyAccountExportDto> exportItems, CancellationToken cancellationToken)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap(map);
        csv.WriteHeader<CurrencyAccountExportDto>();
        await csv.NextRecordAsync();

        await foreach (var exportItem in exportItems.WithCancellation(cancellationToken))
        {
            csv.WriteRecord(exportItem);
            await csv.NextRecordAsync();
        }

        return writer.ToString();
    }
}