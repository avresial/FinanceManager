using System.Globalization;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.CurrencyAccountComponents.Import;

public static class CurrencyImportMapper
{
    public static IEnumerable<(DateTime PostingDate, decimal ValueChange, string? ContractorDetails, string? Description)> MapEntries(
        string postingDateHeader,
        string valueChangeHeader,
        string? contractorDetailsHeader,
        string? descriptionHeader,
        List<string> headers,
        List<List<string>> rows)
    {
        var postingIndex = FindRequiredHeader(headers, postingDateHeader);
        var valueIndex = FindRequiredHeader(headers, valueChangeHeader);
        var contractorIndex = FindOptionalHeader(headers, contractorDetailsHeader);
        var descriptionIndex = FindOptionalHeader(headers, descriptionHeader);

        foreach (var row in rows)
        {
            var posting = GetCell(row, postingIndex);
            var value = GetCell(row, valueIndex);
            var contractor = contractorIndex >= 0 ? GetCell(row, contractorIndex) : null;
            var description = descriptionIndex >= 0 ? GetCell(row, descriptionIndex) : null;

            if (!DateTime.TryParse(posting, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new Exception($"Could not parse posting date: '{posting}'");

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueChange))
                throw new Exception($"Could not parse value change: '{value}'");

            yield return (
                new DateTime(date.Ticks, DateTimeKind.Utc),
                valueChange,
                string.IsNullOrWhiteSpace(contractor) ? null : contractor,
                string.IsNullOrWhiteSpace(description) ? null : description);
        }
    }

    private static int FindRequiredHeader(List<string> headers, string header)
    {
        var index = headers.FindIndex(h => h.Equals(header, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new Exception("Selected headers are invalid.");
    }

    private static int FindOptionalHeader(List<string> headers, string? header) =>
        string.IsNullOrEmpty(header)
            ? -1
            : headers.FindIndex(h => h.Equals(header, StringComparison.OrdinalIgnoreCase));

    private static string GetCell(List<string> row, int index) =>
        index < row.Count ? row[index] : string.Empty;
}
