using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Stocks;

public record AddInvestmentEntryDto : AddFinancialEntryBaseDto
{
    public string Isin { get; }
    public InvestmentType InvestmentType { get; }

    public AddInvestmentEntryDto(DateTime postingDate, decimal valueChange, string isin, InvestmentType investmentType) : base(postingDate, valueChange)
    {
        Isin = isin;
        InvestmentType = investmentType;
    }
}