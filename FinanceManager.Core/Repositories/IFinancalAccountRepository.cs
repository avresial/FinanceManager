using FinanceManager.Core.Entities.Accounts;

namespace FinanceManager.Core.Repositories
{
	public interface IFinancalAccountRepository
	{
		public Dictionary<string, Type> GetAvailableAccounts();
		public IEnumerable<T> GetAccounts<T>(DateTime dateStart, DateTime dateEnd) where T : FinancialAccount;
		public T? GetAccount<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialAccount;
		public List<T>? GetEntries<T>(string name, DateTime dateStart, DateTime dateEnd) where T : FinancialEntryBase;

		public void AddFinancialAccount<T>(T bankAccount) where T : FinancialAccount;
		public void AddFinancialAccount<AccountType, EntryType>(string name, List<EntryType> data) where AccountType : FinancialAccount where EntryType : FinancialEntryBase;
		public bool Exists(string name);
	}
}
