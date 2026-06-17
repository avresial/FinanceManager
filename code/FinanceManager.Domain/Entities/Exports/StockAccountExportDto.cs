using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

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