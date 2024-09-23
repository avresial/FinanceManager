using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities
{
	public class BankAccount
	{
		public string Name { get; set; }
		public List<BankAccountEntry> Entries { get; set; }
		public AccountType AccountType { get; set; }

		public BankAccount(string name, IEnumerable<BankAccountEntry> entries, AccountType accountType)
		{
			Name = name;
			Entries = entries.ToList();
			AccountType = accountType;
		}
		public BankAccount(string name, AccountType accountType)
		{
			Name = name;
			Entries = new List<BankAccountEntry>();
			AccountType = accountType;
		}



	}
}
