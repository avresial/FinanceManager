using CsvHelper;
using FinanceManager.Application.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Bond.Exports;
using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using System.Globalization;
using UserId = int;

namespace FinanceManager.Application.FinancialAccounts.Bond.Export;

public class BondAccountCsvExportService(IBondAccountExportService bondAccountExportService) : IAccountCsvExportService<BondAccountExportDto>
{
    public async Task<string> GetOwnerExportResults(UserId userId, int partitionId, DateTime start, DateTime end, CancellationToken cancellationToken = default) =>
        await WriteCsv(new BondAccountExportDtoMap(), bondAccountExportService.GetOwnerExportResults(userId, partitionId, start, end, cancellationToken), cancellationToken);

    private static async Task<string> WriteCsv(BondAccountExportDtoMap map, IAsyncEnumerable<BondAccountExportDto> exportItems, CancellationToken cancellationToken)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap(map);
        csv.WriteHeader<BondAccountExportDto>();
        await csv.NextRecordAsync();

        await foreach (var exportItem in exportItems.WithCancellation(cancellationToken))
        {
            csv.WriteRecord(exportItem);
            await csv.NextRecordAsync();
        }

        return writer.ToString();
    }
}