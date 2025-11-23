using FinanceManager.Application.Commands.Account;

namespace FinanceManager.Application.Commands.Account;

public record UpdateBondAccountEntry(int AccountId, int EntryId, DateTime PostingDate, 
decimal Value, decimal ValueChange, int BondDetailsId);
