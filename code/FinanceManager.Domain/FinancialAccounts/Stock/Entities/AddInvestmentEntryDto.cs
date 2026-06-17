using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Stock.Entities;

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