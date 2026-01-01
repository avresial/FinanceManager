namespace FinanceManager.Domain.Entities.Stocks;

public record AddStockEntryBaseDto(DateTime PostingDate, decimal ValueChange);