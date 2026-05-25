using CsvHelper;
using FinanceManager.Application.Services.Exports;
using FinanceManager.Application.Services.Exports.Maps;
using FinanceManager.Domain.Entities.Exports;
using System.Globalization;
using AccountId = int;
using UserId = int;

namespace FinanceManager.Application.Services.Stocks;

public class StockAccountCsvExportService(IStockAccountExportService stockAccountExportService) : IAccountCsvExportService<StockAccountExportDto>
{
    public async Task<string> GetExportResults(UserId userId, AccountId accountId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<StockAccountExportDtoMap>();
        csv.WriteHeader<StockAccountExportDto>();
        await csv.NextRecordAsync();

        var exportItems = stockAccountExportService.GetExportResults(userId, accountId, start, end, cancellationToken);
        await foreach (var exportItem in exportItems.WithCancellation(cancellationToken))
        {
            csv.WriteRecord(exportItem);
            await csv.NextRecordAsync();
        }

        return writer.ToString();
    }

    public string GetExportResults(IReadOnlyList<StockAccountExportDto> entries)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<StockAccountExportDtoMap>();
        csv.WriteHeader<StockAccountExportDto>();
        csv.NextRecord();
        csv.WriteRecords(entries);
        return writer.ToString();
    }
}