using FinanceManager.Core.Enums;
using FinanceManager.Core.Repositories;

namespace FinanceManager.Core.Entities.Accounts
{
	public interface IFinancalAccount
	{
		void SetDates(DateTime start, DateTime end);

		void SetDaily();
		void SetMonthly();
		void SetYearly();

		void SetExpenses();
		void SetEarnings();

	}

	public class FinancialAccount : IFinancalAccount
	{
		internal IFinancalAccountRepository _financalAccountRepository;
		public string Name { get; set; }
		public DateTime Start { get; private set; }
		public DateTime End { get; private set; }

		public virtual void SetDates(DateTime start, DateTime end)
		{
			throw new NotImplementedException();
		}

		public void SetDaily()
		{
			throw new NotImplementedException();
		}

		public void SetMonthly()
		{
			throw new NotImplementedException();
		}

		public void SetYearly()
		{
			throw new NotImplementedException();
		}

		public void SetExpenses()
		{
			throw new NotImplementedException();
		}

		public void SetEarnings()
		{
			throw new NotImplementedException();
		}
	}

	public class FixedAssetAccount : FinancialAccount
	{
		public List<FixedAssetEntry>? Entries { get; private set; }
		public override void SetDates(DateTime start, DateTime end)
		{
			if (_financalAccountRepository is null) return;

			Entries = _financalAccountRepository.GetEntries<FixedAssetEntry>(Name, start, end);

			base.SetDates(start, end);
		}
	}
	public class StockAccount : FinancialAccount, IFinancalAccount
	{
		public List<StockEntry>? Entries { get; private set; }
	}

	public class BankAccount : FinancialAccount, IFinancalAccount
	{
		public List<BankAccountEntry>? Entries { get; private set; }
		public AccountType AccountType { get; private set; }

		public BankAccount(string name, IEnumerable<BankAccountEntry> entries, AccountType accountType)
		{
			Name = name;
			Entries = entries.ToList();
			AccountType = accountType;
		}
		public BankAccount(string name, AccountType accountType)
		{
			Name = name;
			AccountType = accountType;
			Entries = new List<BankAccountEntry>();
		}

		public override void SetDates(DateTime start, DateTime end)
		{
			Entries.RemoveAll(x => x.PostingDate < start || x.PostingDate > end);
		}
	}
}
