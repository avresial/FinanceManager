using FinanceManager.Application.Commands.Account;

namespace FinanceManager.Domain.Commands.Account;

public record UpdateCurrencyAccountEntry(int AccountId, int EntryId, DateTime PostingDate, decimal Value, decimal ValueChange,
    string Description, List<UpdateFiancialLabel> Labels);