using System.Globalization;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents.Import;

public static class StockImportMapper
{
    private static readonly string[] AllowedDateFormats =
    [
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy H:mm:ss",
        "MM/dd/yyyy HH:mm:ss",
        "MM/dd/yyyy H:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy/MM/dd HH:mm:ss"
    ];

    public static IEnumerable<(DateTime PostingDate, decimal ValueChange, string Ticker)> MapEntries(
        string postingDateHeader,
        string valueChangeHeader,
        string tickerHeader,
        List<string> headers,
        List<List<string>> rows)
    {
        var postingIndex = FindRequiredHeader(headers, postingDateHeader);
        var valueIndex = FindRequiredHeader(headers, valueChangeHeader);
        var tickerIndex = FindRequiredHeader(headers, tickerHeader);

        foreach (var row in rows)
        {
            var posting = GetCell(row, postingIndex);
            var value = GetCell(row, valueIndex);
            var ticker = GetCell(row, tickerIndex).Trim();

            if (!DateTime.TryParseExact(posting, AllowedDateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                && !DateTime.TryParse(posting, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
                throw new Exception($"Could not parse posting date: '{posting}'");

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueChange))
                throw new Exception($"Could not parse value change: '{value}'");

            if (string.IsNullOrWhiteSpace(ticker))
                throw new Exception("Ticker value is empty.");

            yield return (new DateTime(date.Ticks, DateTimeKind.Utc), valueChange, ticker);
        }
    }

    private static int FindRequiredHeader(List<string> headers, string header)
    {
        var index = headers.FindIndex(h => h.Equals(header, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new Exception("Selected headers are invalid.");
    }

    private static string GetCell(List<string> row, int index) =>
        index < row.Count ? row[index] : string.Empty;
}
