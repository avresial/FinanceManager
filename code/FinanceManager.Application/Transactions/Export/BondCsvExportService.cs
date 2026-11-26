using CsvHelper;
using FinanceManager.Domain.Transactions.Bonds.Exports;
using System.Globalization;
using UserId = int;

namespace FinanceManager.Application.Transactions.Export;

public class BondCsvExportService(IBondExportService bondAccountExportService) : ICsvExportService<BondExportDto>
{
    public async Task<string> GetOwnerExportResults(UserId userId, int partitionId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        await WriteCsv(new BondExportDtoMap(), bondAccountExportService.GetOwnerExportResults(userId, partitionId, start, end, cancellationToken), cancellationToken);

    private static async Task<string> WriteCsv(BondExportDtoMap map, IAsyncEnumerable<BondExportDto> exportItems, CancellationToken cancellationToken)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap(map);
        csv.WriteHeader<BondExportDto>();
        await csv.NextRecordAsync();

        await foreach (var exportItem in exportItems.WithCancellation(cancellationToken))
        {
            csv.WriteRecord(exportItem);
            await csv.NextRecordAsync();
        }

        return writer.ToString();
    }
}