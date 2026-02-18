using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Exports;

public record StockAccountExportDto(
    DateTime PostingDate,
    decimal ValueChange,
    decimal SharesCount,
    decimal Price,
    string Ticker,
    InvestmentType InvestmentType);