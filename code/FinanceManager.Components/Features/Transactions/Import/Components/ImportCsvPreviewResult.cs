namespace FinanceManager.Components.Features.Transactions.Import.Components;

public sealed record ImportCsvPreviewResult(
    List<string> Headers,
    List<List<string>> RawPreview,
    int TotalRowCount);