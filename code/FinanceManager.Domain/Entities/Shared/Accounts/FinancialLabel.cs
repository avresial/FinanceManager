using FinanceManager.Domain.Entities.Cash;

namespace FinanceManager.Domain.Entities.Shared.Accounts;
public class FinancialLabel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<BankAccountEntry> Entries { get; set; } = [];
}
