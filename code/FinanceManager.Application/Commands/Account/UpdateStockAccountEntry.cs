using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Commands.Account;

public record UpdateStockAccountEntry(int AccountId, int EntryId, DateTime PostingDate, decimal Value, decimal ValueChange,
    string Ticker, InvestmentType investmentType, List<UpdateFiancialLabel> Labels);