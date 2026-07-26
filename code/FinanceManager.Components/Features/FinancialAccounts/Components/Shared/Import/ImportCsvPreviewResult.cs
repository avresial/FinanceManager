namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared.Import;

public sealed record ImportCsvPreviewResult(
    List<string> Headers,
    List<List<string>> RawPreview,
    int TotalRowCount);