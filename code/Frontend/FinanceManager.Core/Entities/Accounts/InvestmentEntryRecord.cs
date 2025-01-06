using FinanceManager.Core.Enums;

namespace FinanceManager.Infrastructure.Dtos
{
    public record AddFinancialEntryBaseDto(DateTime PostingDate, decimal ValueChange);
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
}

