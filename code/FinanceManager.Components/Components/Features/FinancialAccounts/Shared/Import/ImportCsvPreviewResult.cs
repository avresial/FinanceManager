namespace FinanceManager.Components.Components.Features.FinancialAccounts.Shared.Import;

public sealed record ImportCsvPreviewResult(
    List<string> Headers,
    List<List<string>> RawPreview,
    int TotalRowCount);