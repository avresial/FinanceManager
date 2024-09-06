using FinanceManager.Core.Entities;

namespace FinanceManager.Core.Repositories
{
	public interface IBankAccountRepository
	{
		public IEnumerable<BankAccount> Get();
		public BankAccount? Get(string name);
		public void AddBankAccount(BankAccount bankAccount);
		public void AddBankAccountEntry(string name, List<BankAccountEntry> data);
		public bool Exists(string name);
	}
}
