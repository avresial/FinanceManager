using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class AccountsService
    {
        private Dictionary<string, List<AccountEntry>> Accounts = new Dictionary<string, List<AccountEntry>>();

        public event Action<string> AccountsChanged;

        public Dictionary<string, List<AccountEntry>> Get() => Accounts;
        public List<AccountEntry> Get(string key) => Accounts[key];
        public void Add(string key, List<AccountEntry> data)
        {
            Accounts.Add(key, data);
            AccountsChanged?.Invoke(key);
        }
        public bool Contains(string key) => Accounts.ContainsKey(key);
    }
}
