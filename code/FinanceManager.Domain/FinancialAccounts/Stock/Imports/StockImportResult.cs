using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
namespace FinanceManager.Domain.FinancialAccounts.Stock.Imports;

public record StockImportResult(int AccountId, int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<StockImportConflict> Conflicts);