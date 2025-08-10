namespace FinanceManager.Domain.Entities.Accounts.Entries;
public class FinancialLabel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<BankAccountEntry> Entries { get; set; }
}
