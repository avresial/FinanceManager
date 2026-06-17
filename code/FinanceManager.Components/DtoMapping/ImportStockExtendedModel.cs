using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Components.DtoMapping;

public class ImportStockExtendedModel : ImportStockModel
{
    public string Ticker { get; set; } = "Unknown";
    public InvestmentType InvestmentType { get; set; }
}