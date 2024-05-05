using FinanceManager.Enums;
using FinanceManager.Models;

namespace FinanceManager.Services
{
    public class AccountsService
    {
        private List<AccountModel> Accounts = new List<AccountModel>();


        public AccountsService()
        {
            Accounts.Add(new AccountModel("Main", new List<AccountEntryDto>() { new AccountEntryDto() { PostingDate = DateTime.Now, Balance = 10 } }, AccountType.Asset));
            Accounts.Add(new AccountModel("Week ago", new List<AccountEntryDto>() { new AccountEntryDto() { PostingDate = DateTime.Now - new TimeSpan(2, 0, 0, 0), Balance = 20 } }, AccountType.Investment));
            Accounts.Add(new AccountModel("Month ago", new List<AccountEntryDto>() { new AccountEntryDto() { PostingDate = DateTime.Now - new TimeSpan(20, 0, 0, 0), Balance = 30 } }, AccountType.Other));
        }


        public event Action<string> AccountsChanged;

        public List<AccountModel> Get()
        {
            List<AccountModel> result = new List<AccountModel>();

            foreach (var item in Accounts)
                result.Add(item);

            return result;
        }
        public AccountModel? Get(string key) => Accounts.FirstOrDefault(x => x.Name == key);
        public void Add(string name, List<AccountEntryDto> data)
        {
            Accounts.Add(new AccountModel(name, data, Enums.AccountType.Cash));
            AccountsChanged?.Invoke(name);
        }
        public bool Contains(string key) => Accounts.Any(x => x.Name == key);
    }
}
