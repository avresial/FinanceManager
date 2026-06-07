using FinanceManager.Domain.Entities.Bonds;
using System.Globalization;

namespace FinanceManager.Components.Components.FinancialAccounts.BondAccountComponents.Import;

public static class BondImportMapper
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

    public static IEnumerable<(DateTime PostingDate, decimal ValueChange, int BondDetailsId, string BondName)> MapEntries(
        string postingDateHeader,
        string valueChangeHeader,
        string bondHeader,
        List<string> headers,
        List<List<string>> rows,
        IReadOnlyCollection<BondDetails> possibleBonds)
    {
        var postingIndex = FindRequiredHeader(headers, postingDateHeader);
        var valueIndex = FindRequiredHeader(headers, valueChangeHeader);
        var bondIndex = FindRequiredHeader(headers, bondHeader);

        foreach (var row in rows)
        {
            var posting = GetCell(row, postingIndex);
            var value = GetCell(row, valueIndex);
            var bond = GetCell(row, bondIndex);

            if (!DateTime.TryParseExact(posting, AllowedDateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                && !DateTime.TryParse(posting, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
                throw new Exception($"Could not parse posting date: '{posting}'");

            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueChange))
                throw new Exception($"Could not parse value change: '{value}'");

            if (string.IsNullOrWhiteSpace(bond))
                throw new Exception("Bond value is empty.");

            var resolvedBond = ResolveBond(bond, possibleBonds);
            if (resolvedBond is null)
                throw new Exception($"Could not map bond '{bond}' to existing bond details (id or name).");

            yield return (new DateTime(date.Ticks, DateTimeKind.Utc), valueChange, resolvedBond.Id, resolvedBond.Name);
        }
    }

    private static BondDetails? ResolveBond(string bondValue, IReadOnlyCollection<BondDetails> possibleBonds)
    {
        var normalized = bondValue.Trim();
        if (int.TryParse(normalized, out var id))
            return possibleBonds.FirstOrDefault(x => x.Id == id);

        return possibleBonds.FirstOrDefault(x => string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static int FindRequiredHeader(List<string> headers, string header)
    {
        var index = headers.FindIndex(h => h.Equals(header, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new Exception("Selected headers are invalid.");
    }

    private static string GetCell(List<string> row, int index) =>
        index < row.Count ? row[index] : string.Empty;
}
