using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos
{
    public class StockAccountEntryDto : FinancialEntryBaseDto
    {
        public required string Ticker { get; set; }
        public InvestmentType InvestmentType { get; set; }
    }
}
