namespace FinanceManager.Components.Components.Features.FinancialAccounts.Shared.Import;

public static class ImportCsvPreviewReader
{
    public static async Task<ImportCsvPreviewResult?> ReadAsync(
        string content,
        string delimiter,
        Func<string, string, CancellationToken, Task<(List<string> Headers, List<List<string>> Data)?>> readCsv,
        CancellationToken cancellationToken = default)
    {
        var result = await readCsv(content, delimiter, cancellationToken);
        if (result is null)
            return null;

        var headers = result.Value.Headers ?? [];
        var allRows = result.Value.Data ?? [];
        var rawPreview = headers.Count != 0 && allRows.Count != 0
            ? allRows.Take(3).Select(row => row.ToList()).ToList()
            : [];

        RemoveEmptyPreviewColumns(headers, rawPreview);

        return new ImportCsvPreviewResult(headers, rawPreview, allRows.Count);
    }

    private static void RemoveEmptyPreviewColumns(List<string> headers, List<List<string>> rawPreview)
    {
        if (rawPreview.Count == 0)
            return;

        var emptyIndexes = rawPreview[0]
            .Select((value, index) => new { value, index })
            .Where(x => string.IsNullOrEmpty(x.value))
            .Select(x => x.index)
            .ToList();

        foreach (var previewItem in rawPreview.Skip(1))
        {
            var rowEmptyIndexes = previewItem
                .Select((value, index) => new { value, index })
                .Where(x => string.IsNullOrEmpty(x.value))
                .Select(x => x.index)
                .ToHashSet();

            emptyIndexes.RemoveAll(index => !rowEmptyIndexes.Contains(index));
        }

        foreach (var index in emptyIndexes.OrderByDescending(x => x))
        {
            if (index < headers.Count)
                headers.RemoveAt(index);

            foreach (var previewItem in rawPreview)
            {
                if (index < previewItem.Count)
                    previewItem.RemoveAt(index);
            }
        }
    }
}