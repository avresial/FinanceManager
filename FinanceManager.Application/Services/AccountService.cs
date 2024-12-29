using FinanceManager.Core.Entities.Accounts;
using FinanceManager.Core.Repositories;
using FinanceManager.Core.Services;

namespace FinanceManager.Application.Services
{
    public class AccountService(IFinancalAccountRepository bankAccountRepository) : IAccountService
    {
        private readonly IFinancalAccountRepository _bankAccountRepository = bankAccountRepository;

        public event Action? AccountsChanged;
        public Dictionary<int, Type> GetAvailableAccounts()
        {
            return _bankAccountRepository.GetAvailableAccounts();
        }
        public int GetLastAccountId() => _bankAccountRepository.GetLastAccountId();
        public DateTime? GetStartDate(int id)
        {
            return _bankAccountRepository?.GetStartDate(id);
        }
        public DateTime? GetEndDate(int id)
        {
            return _bankAccountRepository?.GetEndDate(id);
        }

        public bool AccountExists(int id)
        {
            return _bankAccountRepository.AccountExists(id);
        }
        public T? GetAccount<T>(int id, DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase
        {
            if (_bankAccountRepository is null) throw new Exception();
            return _bankAccountRepository.GetAccount<T>(id, dateStart, dateEnd);
        }
        public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccountBase
        {
            return _bankAccountRepository.GetAccounts<T>(dateStart, dateEnd);
        }
        public void AddAccount<T>(T bankAccount) where T : FinancialAccountBase
        {
            _bankAccountRepository?.AddAccount(bankAccount);
            AccountsChanged?.Invoke();
        }
        public void AddAccount<AccountType, EntryType>(string name, List<EntryType> data) where AccountType : FinancialAccountBase where EntryType : FinancialEntryBase
        {
            _bankAccountRepository?.AddAccount<AccountType, EntryType>(name, data);
        }
        public void UpdateAccount<T>(T account) where T : FinancialAccountBase
        {
            _bankAccountRepository?.UpdateAccount(account);
        }
        public void RemoveAccount(int id)
        {
            _bankAccountRepository?.RemoveAccount(id);
        }

        public void AddEntry<T>(T bankAccount, int id) where T : FinancialEntryBase
        {
            _bankAccountRepository.AddEntry<T>(bankAccount, id);
            AccountsChanged?.Invoke();
        }
        public void UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
        {
            _bankAccountRepository.UpdateEntry<T>(accountEntry, id);
            AccountsChanged?.Invoke();
        }
        public void RemoveEntry(int accountEntryId, int id)
        {
            _bankAccountRepository.RemoveEntry(accountEntryId, id);
            AccountsChanged?.Invoke();
        }
    }
}
