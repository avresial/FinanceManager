using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Components.Features.FinancialAccounts.DtoMapping;

public class ImportStockExtendedModel : ImportStockModel
{
    public string Ticker { get; set; } = "Unknown";
    public InvestmentType InvestmentType { get; set; }
}