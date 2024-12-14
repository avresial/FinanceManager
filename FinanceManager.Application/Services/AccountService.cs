using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
    public class AccountService(IFinancalAccountRepository bankAccountRepository) : IAccountService
    {
        private readonly IFinancalAccountRepository _bankAccountRepository = bankAccountRepository;

        public event Action? AccountsChanged;
        public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase
        {
            return _bankAccountRepository.GetAccounts<T>(dateStart, dateEnd);
        }
        public T? GetAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase
        {
            if (_bankAccountRepository is null) throw new Exception();
            return _bankAccountRepository.GetAccount<T>(name, dateStart, dateEnd);
        }
        public DateTime? GetStartDate(string name)
        {
            return _bankAccountRepository?.GetStartDate(name);
        }

        public DateTime? GetEndDate(string name)
        {
            return _bankAccountRepository?.GetEndDate(name);
        }
        public List<T>? GetEntries<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialEntryBase
        {
            return null;
        }
        public void AddFinancialAccount<T>(T bankAccount) where T : FinancialAccountBase
        {
            _bankAccountRepository?.AddFinancialAccount(bankAccount);
            AccountsChanged?.Invoke();
        }

        public void AddFinancialAccount<AccountType, EntryType>(string name, List<EntryType> data) where AccountType : FinancialAccountBase where EntryType : FinancialEntryBase
        {
            _bankAccountRepository?.AddFinancialAccount<AccountType, EntryType>(name, data);
        }

        public bool AccountExists(string name)
        {
            return _bankAccountRepository.AccountExists(name);
        }

        public Dictionary<string, Type> GetAvailableAccounts()
        {
            return _bankAccountRepository.GetAvailableAccounts();
        }

        public void AddFinancialEntry<T>(T bankAccount, string accountName) where T : FinancialEntryBase
        {
            _bankAccountRepository.AddFinancialEntry<T>(bankAccount, accountName);
            AccountsChanged?.Invoke();
        }

        public void UpdateFinancialEntry<T>(T accountEntry, string accountName) where T : FinancialEntryBase
        {
            _bankAccountRepository.UpdateFinancialEntry<T>(accountEntry, accountName);
            AccountsChanged?.Invoke();
        }

        public void RemoveFinancialEntry(int accountEntryId, string accountName)
        {
            _bankAccountRepository.RemoveFinancialEntry(accountEntryId, accountName);
            AccountsChanged?.Invoke();
        }

        public int GetLastAccountId() => _bankAccountRepository.GetLastAccountId();


    }
}
