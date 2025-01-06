using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
    public record AddInvestmentEntryDto(DateTime PostingDate, decimal ValueChange, string Ticker, InvestmentType InvestmentType);
}

