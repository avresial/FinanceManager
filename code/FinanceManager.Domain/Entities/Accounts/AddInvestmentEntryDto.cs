using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Accounts
{
    public record AddInvestmentEntryDto(DateTime PostingDate, decimal ValueChange, string Ticker, InvestmentType InvestmentType);
}

