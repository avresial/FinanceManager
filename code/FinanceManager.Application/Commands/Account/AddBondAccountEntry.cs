namespace FinanceManager.Application.Commands.Account;

public record AddBondAccountEntry(int AccountId, int EntryId, DateTime PostingDate, decimal Value, decimal ValueChange, int BondDetailsId);