using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Exports;

public record StockAccountExportDto(
    int Id,
    DateTime PostingDate,
    decimal ValueChange,
    decimal SharesCount,
    decimal Price,
    string Ticker,
    InvestmentType InvestmentType,
    string? Labels = null) : IAccountExportDto;