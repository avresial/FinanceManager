using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos
{
    public class ImportStockModel
    {
        public DateTime PostingDate { get; set; }
        public decimal ValueChange { get; set; }
    }

    public class ImportStockExtendedModel : ImportStockModel
    {
        public string Ticker { get; set; } = "Unknown";
        public InvestmentType InvestmentType { get; set; }
    }

}
