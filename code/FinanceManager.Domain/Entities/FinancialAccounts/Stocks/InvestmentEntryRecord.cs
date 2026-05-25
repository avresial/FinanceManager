namespace FinanceManager.Domain.Entities.Stocks;

public record AddFinancialEntryBaseDto(DateTime PostingDate, decimal ValueChange);