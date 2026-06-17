using FinanceManager.Domain.Dtos;
using FinanceManager.Domain.FinancialAccounts.Stock.Dtos;

namespace FinanceManager.Application.FinancialAccounts.Stock.Import;

public interface IStockPriceBulkImportService
{
    Task<StockPriceBulkImportResultDto> ImportClosePrices(Stream csvStream, CancellationToken ct = default);
}