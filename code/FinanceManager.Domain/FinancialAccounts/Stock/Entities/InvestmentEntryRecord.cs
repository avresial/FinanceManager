namespace FinanceManager.Domain.FinancialAccounts.Stock.Entities;

public record AddFinancialEntryBaseDto(DateTime PostingDate, decimal ValueChange);