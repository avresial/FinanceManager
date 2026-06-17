using FinanceManager.Domain.Entities.Exports;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Exports;

public record StockAccountExportDto(
    int Id,
    DateTime PostingDate,
    decimal ValueChange,
    decimal SharesCount,
    decimal Price,
    string Ticker,
    InvestmentType InvestmentType,
    string? Labels = null) : IAccountExportDto;