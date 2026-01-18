namespace FinanceManager.Application.Commands.Account;

public record UpdateBankAccountEntry(int AccountId, int EntryId, DateTime PostingDate, decimal Value, decimal ValueChange,
    string Description, List<UpdateFiancialLabel> Labels);