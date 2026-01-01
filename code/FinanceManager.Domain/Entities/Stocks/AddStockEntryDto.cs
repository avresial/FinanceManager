using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Stocks;

public record AddStockEntryDto : AddStockEntryBaseDto
{
    public string Ticker { get; }
    public InvestmentType InvestmentType { get; }

    public AddStockEntryDto(DateTime postingDate, decimal valueChange, string ticker, InvestmentType investmentType) : base(postingDate, valueChange)
    {
        Ticker = ticker;
        InvestmentType = investmentType;
    }
}