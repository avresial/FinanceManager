using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Shared.Dtos;

public class FinancialAccountBaseDto
{
    public AccountType AccountType { get; set; }
    public AccountLabel AccountLabel { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
};