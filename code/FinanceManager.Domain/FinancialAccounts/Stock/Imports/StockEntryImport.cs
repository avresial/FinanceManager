using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
namespace FinanceManager.Domain.FinancialAccounts.Stock.Imports;

public record StockEntryImport(DateTime PostingDate, decimal ValueChange, string Isin);