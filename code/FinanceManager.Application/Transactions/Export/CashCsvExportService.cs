using CsvHelper;
using FinanceManager.Domain.Transactions.Cash.Exports;
using System.Globalization;
using UserId = int;

namespace FinanceManager.Application.Transactions.Export;

public class CashCsvExportService(ICashExportService currencyAccountExportService) : ICsvExportService<CashExportDto>
{
    public async Task<string> GetOwnerExportResults(UserId userId, int partitionId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        await WriteCsv(new CashExportDtoMap(), currencyAccountExportService.GetOwnerExportResults(userId, partitionId, start, end, cancellationToken), cancellationToken);

    private static async Task<string> WriteCsv(CashExportDtoMap map, IAsyncEnumerable<CashExportDto> exportItems, CancellationToken cancellationToken)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap(map);
        csv.WriteHeader<CashExportDto>();
        await csv.NextRecordAsync();

        await foreach (var exportItem in exportItems.WithCancellation(cancellationToken))
        {
            csv.WriteRecord(exportItem);
            await csv.NextRecordAsync();
        }

        return writer.ToString();
    }
}