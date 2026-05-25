namespace FinanceManager.Domain.Commands.Account;

public record AddCurrencyAccountEntry(int AccountId, int EntryId, DateTime PostingDate, decimal Value, decimal ValueChange, string Description, string? ContractorDetails);