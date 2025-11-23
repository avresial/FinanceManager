using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Enums;
using System.Text.Json.Serialization;

namespace FinanceManager.Domain.Entities.Bonds;

public class BondAccount : FinancialAccountBase<BondAccountEntry>
{
    public readonly BondAccountEntry? NextOlderEntry = null;
    public readonly BondAccountEntry? NextYoungerEntry = null;
    public AccountLabel AccountType { get; set; }

    [JsonConstructor]
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