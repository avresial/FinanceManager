using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using System.Globalization;

namespace FinanceManager.Application.FinancialAccounts.Stock.Import;

internal sealed class StockPriceBulkImportService(
    IStockDetailsRepository stockDetailsRepository,
    IStockPriceRepository stockPriceRepository) : IStockPriceBulkImportService
{
    private const string _tickerHeader = "TICKER";
    private const string _dateHeader = "DATE";
    private const string _closeHeader = "CLOSE";

    public async Task<StockPriceBulkImportResultDto> ImportClosePrices(Stream csvStream, CancellationToken ct = default)
    {
        var stockDetails = await stockDetailsRepository.GetAll(ct);
        var stockByTicker = stockDetails
            .Where(x => !string.IsNullOrWhiteSpace(x.Ticker))
            .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
            .ToDictionary(x => x.Key, x => x.First());

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        });

        if (!await csv.ReadAsync())
            throw new InvalidOperationException("CSV header row is missing.");

        csv.ReadHeader();

        var headers = csv.HeaderRecord;
        if (headers is null || headers.Length == 0)
            throw new InvalidOperationException("CSV headers are missing.");

        var tickerIndex = FindHeaderIndex(headers, _tickerHeader);
        var dateIndex = FindHeaderIndex(headers, _dateHeader);
        var closeIndex = FindHeaderIndex(headers, _closeHeader);

        if (tickerIndex < 0 || dateIndex < 0 || closeIndex < 0)
            throw new InvalidOperationException("CSV must contain TICKER, DATE and CLOSE columns.");

        var pricesToUpsert = new List<StockPrice>();
        var unknownTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var totalRows = 0;
        var importedOrUpdatedRows = 0;
        var skippedUnknownTickerRows = 0;
        var invalidRows = 0;

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();

            totalRows++;

            var rawTicker = csv.GetField(tickerIndex) ?? string.Empty;
            var rawDate = csv.GetField(dateIndex) ?? string.Empty;
            var rawClose = csv.GetField(closeIndex) ?? string.Empty;

            var ticker = NormalizeTicker(rawTicker);
            if (string.IsNullOrWhiteSpace(ticker))
            {
                invalidRows++;
                continue;
            }

            if (!stockByTicker.TryGetValue(ticker, out var stockDetailsForTicker))
            {
                skippedUnknownTickerRows++;
                unknownTickers.Add(ticker);
                continue;
            }

            if (!TryParseDate(rawDate, out var parsedDate) || !TryParseDecimal(rawClose, out var closePrice) || closePrice <= 0)
            {
                invalidRows++;
                continue;
            }

            pricesToUpsert.Add(new StockPrice
            {
                Isin = stockDetailsForTicker.Isin,
                Date = parsedDate,
                PricePerUnit = closePrice,
                Currency = stockDetailsForTicker.Currency
            });

            importedOrUpdatedRows++;
        }

        if (pricesToUpsert.Count > 0)
            await stockPriceRepository.Add(pricesToUpsert);

        return new StockPriceBulkImportResultDto(
            TotalRows: totalRows,
            ImportedOrUpdatedRows: importedOrUpdatedRows,
            SkippedUnknownTickerRows: skippedUnknownTickerRows,
            InvalidRows: invalidRows,
            UnknownTickers: unknownTickers.OrderBy(x => x).ToList());
    }

    private static int FindHeaderIndex(string[] headers, string expectedHeader)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var normalized = NormalizeHeader(headers[i]);
            if (normalized == expectedHeader)
                return i;
        }

        return -1;
    }

    private static string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;

        return header.Trim().Trim('<', '>').ToUpperInvariant();
    }

    private static string NormalizeTicker(string ticker)
        => ticker.Trim().ToUpperInvariant();

    private static bool TryParseDate(string rawDate, out DateTime parsedDate)
    {
        return DateTime.TryParseExact(
            rawDate.Trim(),
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsedDate);
    }

    private static bool TryParseDecimal(string rawDecimal, out decimal parsed)
    {
        return decimal.TryParse(rawDecimal.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out parsed);
    }
}