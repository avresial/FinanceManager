using FinanceManager.Domain.Shared.Services;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;
using FinanceManager.Domain.Transactions.Shared.Dtos;
using System.Globalization;
using System.Text;

namespace FinanceManager.Application.Insights.Generation;

/// <summary>
/// Builds the CSV context sent to the AI provider from the owner's recent cash and bond transactions.
/// It sends no more than before the accounts were removed: no labels, no trades, bounded rows and characters.
/// </summary>
internal sealed class InsightsContextBuilder(
    ICashTransactionReader cashTransactionReader,
    IBondTransactionReader bondTransactionReader,
    IDateTimeProvider dateTimeProvider)
{
    private const int _maxCashRows = 250;
    private const int _maxBondRows = 200;
    private const int _maxSectionChars = 44_000;

    public async Task<string> BuildEntriesContextCsv(int userId, CancellationToken cancellationToken = default)
    {
        var end = dateTimeProvider.UtcNow;
        var start = end.AddMonths(-3);
        var inv = CultureInfo.InvariantCulture;

        var sb = new StringBuilder();
        sb.AppendLine($"timeRangeUtcStart,{start:O}");
        sb.AppendLine($"timeRangeUtcEnd,{end:O}");

        // Zero is not a real owner; guests (negative ids) are real owners with their own seeded data.
        if (userId == 0)
            return sb.ToString();

        var cashQuery = new OwnerHistoryQuery(start, end, _maxCashRows, IncludeDetails: false);
        var cash = await cashTransactionReader.GetHistory(userId, cashQuery, cancellationToken);
        AppendSection(sb, "cash", "PostingDate,Currency,ValueChange,ContractorDetails,Description",
            cash.Select(e => string.Join(',',
                e.PostingDate.ToString("yyyy-MM-dd", inv),
                EscapeCsvValue(e.Currency),
                e.ValueChange.ToString(inv),
                EscapeCsvValue(e.ContractorDetails),
                EscapeCsvValue(e.Description))));

        var bondQuery = new OwnerHistoryQuery(start, end, _maxBondRows, IncludeDetails: false);
        var bonds = await bondTransactionReader.GetHistory(userId, bondQuery, cancellationToken);
        AppendSection(sb, "bonds", "PostingDate,BondName,Currency,ValueChange,Description",
            bonds.Select(e => string.Join(',',
                e.PostingDate.ToString("yyyy-MM-dd", inv),
                EscapeCsvValue(e.BondName),
                EscapeCsvValue(e.Currency),
                e.ValueChange.ToString(inv),
                EscapeCsvValue(e.Description))));

        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string name, string header, IEnumerable<string> rows)
    {
        var csv = new StringBuilder();
        foreach (var row in rows)
        {
            if (csv.Length + row.Length + 1 > _maxSectionChars)
                break;

            csv.AppendLine(row);
        }

        // A section without rows is omitted entirely.
        if (csv.Length == 0)
            return;

        sb.AppendLine();
        sb.AppendLine($"[{name}]");
        sb.AppendLine("csv:");
        sb.AppendLine(header);
        sb.Append(csv);
    }

    private static string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}