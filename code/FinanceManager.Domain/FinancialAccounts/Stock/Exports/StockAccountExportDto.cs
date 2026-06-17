using FinanceManager.Domain.FinancialAccounts.Shared.Exports;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Entities;

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