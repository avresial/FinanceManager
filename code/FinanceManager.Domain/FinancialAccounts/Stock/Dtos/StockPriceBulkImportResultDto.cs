namespace FinanceManager.Domain.FinancialAccounts.Stock.Dtos;

public sealed record StockPriceBulkImportResultDto(
    int TotalRows,
    int ImportedOrUpdatedRows,
    int SkippedUnknownTickerRows,
    int InvalidRows,
    IReadOnlyList<string> UnknownTickers);