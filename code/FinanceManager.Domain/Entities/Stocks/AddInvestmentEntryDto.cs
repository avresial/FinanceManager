using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Stocks;

public record AddInvestmentEntryDto : AddFinancialEntryBaseDto
{
    public string Ticker { get; }
    public InvestmentType InvestmentType { get; }

    public AddInvestmentEntryDto(DateTime postingDate, decimal valueChange, string ticker, InvestmentType investmentType) : base(postingDate, valueChange)
    {
        Ticker = ticker;
        InvestmentType = investmentType;
    }
}