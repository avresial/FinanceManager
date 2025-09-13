namespace FinanceManager.Domain.Entities.Accounts.Entries;

public record AddFinancialEntryBaseDto(DateTime PostingDate, decimal ValueChange);