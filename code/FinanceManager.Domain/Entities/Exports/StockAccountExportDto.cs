using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Exports;

public record StockAccountExportDto(
    DateTime PostingDate,
    decimal ValueChange,
    string Ticker,
    InvestmentType InvestmentType);