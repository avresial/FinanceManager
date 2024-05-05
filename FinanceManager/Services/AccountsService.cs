using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class AccountsService
    {
        private Dictionary<string, List<AccountEntryDto>> Accounts = new Dictionary<string, List<AccountEntryDto>>();


        public AccountsService()
        {
            Accounts.Add("Main", new List<AccountEntryDto>() { new AccountEntryDto() { PostingDate = DateTime.Now , Balance = 10 } });
            Accounts.Add("Week ago", new List<AccountEntryDto>() { new AccountEntryDto() { PostingDate = DateTime.Now - new TimeSpan(2,0, 0, 0), Balance = 20 } });
            Accounts.Add("Month ago", new List<AccountEntryDto>() { new AccountEntryDto() { PostingDate = DateTime.Now - new TimeSpan(20,0, 0, 0), Balance = 30 } });
        }


        public event Action<string> AccountsChanged;

        public Dictionary<string, List<AccountEntryDto>> Get() 
        {
            Dictionary<string, List<AccountEntryDto>> result = new Dictionary<string, List<AccountEntryDto>>();
            foreach (var item in Accounts)
            {
                result.Add(item.Key, item.Value);
            }

            return result;
        }
        public List<AccountEntryDto> Get(string key) => Accounts[key];
        public void Add(string key, List<AccountEntryDto> data)
        {
            Accounts.Add(key, data);
            AccountsChanged?.Invoke(key);
        }
        public bool Contains(string key) => Accounts.ContainsKey(key);
    }
}
