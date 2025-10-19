using CsvHelper;
using CsvHelper.Configuration;
using FinanceManager.Infrastructure.DtoMapping;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.AspNetCore.Components.Forms;
using System.Globalization;

namespace FinanceManager.Infrastructure.Readers;

public static class ImportBankModelReader
{
    public static async Task<List<ImportBankModel>> Read(CsvConfiguration config, IBrowserFile file, string postingDateHeader, string valueChangeHeader, CancellationToken cancellationToken = default)
    {
        var result = new List<ImportBankModel>();
        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap(new ImportBankModelMap(postingDateHeader, valueChangeHeader));

        // iterate rows and support cancellation
        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = csv.GetRecord<ImportBankModel>();
            result.Add(record);
        }

        return result;
    }

    public static async Task<(List<string> Headers, List<List<string>> Data)?> Read(string content, string delimiter, CancellationToken cancellationToken = default)
    {
        var allLines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').ToList();

        if (allLines.Count == 0)
            throw new Exception("File does not contain any lines.");

        var joinedContent = string.Join("\n", allLines);


        using StringReader stringReader = new(joinedContent);
        CsvConfiguration cfg = new(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            BadDataFound = null
        };

        using CsvReader csv = new(stringReader, cfg);

        if (!await csv.ReadAsync().ConfigureAwait(false))
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        csv.ReadHeader();
        var headerRecord = csv.HeaderRecord;
        if (headerRecord == null || headerRecord.Length == 0)
            return null;

        var headers = headerRecord.Select(h => CleanData(h?.Trim() ?? string.Empty)).ToList();

        List<List<string>> allParsedRows = new();

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new List<string>();
            for (int i = 0; i < headers.Count; i++)
            {
                try
                {
                    var raw = csv.GetField(i) ?? string.Empty;
                    row.Add(CleanData(raw));
                }
                catch
                {
                    row.Add(string.Empty);
                }
            }

            allParsedRows.Add(row);
        }

        return (headers, allParsedRows);
    }
    public static string CleanData(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s ?? string.Empty;

        s = s.Trim();
        s = s.TrimStart('\uFEFF').Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
        {
            var inner = s.Substring(1, s.Length - 2);
            return inner.Replace("\"\"", "\"").Trim('\uFEFF', '\u00A0');
        }

        return s.Replace("\"\"", "\"").Trim('\uFEFF', '\u00A0');
    }
}