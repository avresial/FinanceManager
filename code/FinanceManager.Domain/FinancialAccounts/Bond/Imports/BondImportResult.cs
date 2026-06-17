using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Imports;

public record BondImportResult(int AccountId, int Imported, int Failed, IReadOnlyList<string> Errors, IReadOnlyList<BondImportConflict> Conflicts);