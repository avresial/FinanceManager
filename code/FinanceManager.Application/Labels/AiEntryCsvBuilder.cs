using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using System.Globalization;
using System.Text;

namespace FinanceManager.Application.Labels;

internal static class AiEntryCsvBuilder
{
    private const string _labelingHeader = "entryId,postingDate,valueChange,description,contractorDetails";
    private const string _suggestionHeader = "postingDate,valueChange,description,contractorDetails";

    public static string BuildForCurrencyLabeling(IEnumerable<CurrencyAccountEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_labelingHeader);
        foreach (var entry in entries)
        {
            sb.Append(entry.EntryId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(entry.PostingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(entry.ValueChange.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Escape(entry.Description)).Append(',');
            sb.Append(Escape(entry.ContractorDetails));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    public static string BuildForLabelSuggestion(IEnumerable<CurrencyAccountEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_suggestionHeader);
        foreach (var entry in entries)
        {
            sb.Append(entry.PostingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(entry.ValueChange.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Escape(entry.Description)).Append(',');
            sb.Append(Escape(entry.ContractorDetails));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0) return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}