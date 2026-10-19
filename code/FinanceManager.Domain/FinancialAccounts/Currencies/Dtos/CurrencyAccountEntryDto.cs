using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;

namespace FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;

public class CurrencyAccountEntryDto : FinancialEntryBaseDto
{
    public string Description { get; set; } = string.Empty;
    public string? ContractorDetails { get; set; }

}