using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities.Bonds;

public class BondAccount : FinancialAccountBase<BondAccountEntry>
{
    public BondAccountEntry? NextOlderEntry { get; set; }
    public BondAccountEntry? NextYoungerEntry { get; set; }
    public AccountLabel AccountType { get; set; }

    public BondAccount(int userId, int accountId, string name, IEnumerable<BondAccountEntry>? entries = null, AccountLabel accountType = AccountLabel.Other,
        BondAccountEntry? nextOlderEntry = null, BondAccountEntry? nextYoungerEntry = null) : base(userId, accountId, name)
    {
        this.UserId = userId;
        Entries = entries is null ? ([]) : entries.ToList();
        AccountType = accountType;
        NextOlderEntry = nextOlderEntry;
        NextYoungerEntry = nextYoungerEntry;
    }

    public BondAccount(int userId, int id, string name, AccountLabel accountType) : base(userId, id, name)
    {
        AccountType = accountType;
        Entries = [];
    }
}