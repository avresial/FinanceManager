using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceManager.Components.Components.FinancialAccounts.Shared.Import;

public partial class ImportCsvFileStep : ComponentBase
{
    [Parameter] public IReadOnlyCollection<string> Errors { get; set; } = [];
    [Parameter] public IReadOnlyList<List<string>> RawPreview { get; set; } = [];
    [Parameter] public IReadOnlyList<string> Headers { get; set; } = [];
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string? FileName { get; set; }
    [Parameter] public long FileSize { get; set; }
    [Parameter] public int TotalRowCount { get; set; }
    [Parameter] public string Description { get; set; } = string.Empty;
    [Parameter] public string DragClass { get; set; } = string.Empty;
    [Parameter] public string Delimiter { get; set; } = ",";
    [Parameter] public EventCallback<string> DelimiterChanged { get; set; }
    [Parameter] public EventCallback<InputFileChangeEventArgs> OnFilesChanged { get; set; }
    [Parameter] public EventCallback Clear { get; set; }
    [Parameter] public EventCallback SetDragClass { get; set; }
    [Parameter] public EventCallback ClearDragClass { get; set; }

    private static string Truncate(string value)
    {
        var rowContent = new string(value.Take(20).ToArray());
        return rowContent.Length == 20 ? $"{rowContent}..." : rowContent;
    }
}
