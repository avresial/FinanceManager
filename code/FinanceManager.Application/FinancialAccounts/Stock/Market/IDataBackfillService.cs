namespace FinanceManager.Application.FinancialAccounts.Stock.Market;

/// <summary>
/// Service for backfilling data during schema migrations.
/// Used in Phase 3b to populate ISIN values from existing Tickers.
/// </summary>
public interface IDataBackfillService
{
    /// <summary>
    /// Backfill ISIN values for all StockDetails records using OpenFIGI.
    /// </summary>
    /// <returns>Number of records successfully backfilled</returns>
    Task<int> BackfillStockDetailsIsinAsync(CancellationToken ct = default);

    /// <summary>
    /// Backfill ISIN values for all StockEntries from their corresponding StockDetails.
    /// </summary>
    /// <returns>Number of records successfully backfilled</returns>
    Task<int> BackfillStockEntriesIsinAsync(CancellationToken ct = default);
}