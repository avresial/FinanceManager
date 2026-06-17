using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
namespace FinanceManager.Domain.FinancialAccounts.Bond.Imports;

public record BondEntryImport(DateTime PostingDate, decimal ValueChange, int BondDetailsId);