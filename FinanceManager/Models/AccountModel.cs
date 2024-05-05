namespace FinanceManager.Models
{
    public class AccountModel
    {
        public AccountModel(string name, IEnumerable<AccountEntry> entries)
        {
            Name = name;
            Entries = entries;
        }

        public string Name { get; set; }
        public IEnumerable<AccountEntry> Entries { get; set; }
    }
}
