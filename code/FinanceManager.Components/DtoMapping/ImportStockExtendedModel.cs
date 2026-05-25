using FinanceManager.Domain.Enums;

namespace FinanceManager.Infrastructure.Dtos;

public class ImportStockExtendedModel : ImportStockModel
{
    public string Ticker { get; set; } = "Unknown";
    public InvestmentType InvestmentType { get; set; }
}