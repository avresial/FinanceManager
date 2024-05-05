using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class AccountsService
    {
        private Dictionary<string, List<AccountEntry>> Accounts = new Dictionary<string, List<AccountEntry>>();


        public AccountsService()
        {
            Accounts.Add("Main", new List<AccountEntry>() { new AccountEntry() { PostingDate = DateTime.Now , Balance = 10 } });
            Accounts.Add("Week ago", new List<AccountEntry>() { new AccountEntry() { PostingDate = DateTime.Now - new TimeSpan(7,0, 0, 0), Balance = 20 } });
            Accounts.Add("Month ago", new List<AccountEntry>() { new AccountEntry() { PostingDate = DateTime.Now - new TimeSpan(31,0, 0, 0), Balance = 20 } });
        }


        public event Action<string> AccountsChanged;

        public Dictionary<string, List<AccountEntry>> Get() 
        {
            Dictionary<string, List<AccountEntry>> result = new Dictionary<string, List<AccountEntry>>();
            foreach (var item in Accounts)
            {
                result.Add(item.Key, item.Value);
            }

            return result;
        }
        public List<AccountEntry> Get(string key) => Accounts[key];
        public void Add(string key, List<AccountEntry> data)
        {
            Accounts.Add(key, data);
            AccountsChanged?.Invoke(key);
        }
        public bool Contains(string key) => Accounts.ContainsKey(key);
    }
}
