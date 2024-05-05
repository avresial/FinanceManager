using FinanceManager.Enums;

namespace FinanceManager.Models
{
    public class AccountModel
    {
        public string Name { get; set; }
        public IEnumerable<AccountEntryDto> Entries { get; set; }
        public AccountType AccountType { get; set; }

        public AccountModel(string name, IEnumerable<AccountEntryDto> entries, AccountType accountType)
        {
            Name = name;
            Entries = entries;
            AccountType = accountType;
        }
    }
}
