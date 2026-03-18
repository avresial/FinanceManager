using FinanceManager.Domain.Dtos;

namespace FinanceManager.Application.Services.Stocks;

public interface IStockPriceBulkImportService
{
    Task<StockPriceBulkImportResultDto> ImportClosePrices(Stream csvStream, CancellationToken ct = default);
}
