using FinanceManager.Domain.Dtos;

namespace FinanceManager.Application.FinancialAccounts.Stock.Import;

public interface IStockPriceBulkImportService
{
    Task<StockPriceBulkImportResultDto> ImportClosePrices(Stream csvStream, CancellationToken ct = default);
}