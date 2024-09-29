using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities.Accounts
{
	public class FinancialEntryBase
	{
		public FinancialEntryBase(DateTime postingDate, decimal value, decimal valueChange)
		{
			PostingDate = postingDate;
			Value = value;
			ValueChange = valueChange;
		}

		public DateTime PostingDate { get; internal set; }
		public decimal Value { get; internal set; }
		public decimal ValueChange { get; internal set; }
	}
	public class StockEntry : FinancialEntryBase
	{
		public string Ticker { get; set; }

		public StockEntry(DateTime postingDate, decimal value, decimal valueChange, string ticker) : base(postingDate, value, valueChange)
		{
			Ticker = ticker;
		}

	}

	public class FixedAssetEntry : FinancialEntryBase
	{
		public string Name { get; set; }
		public string Currency { get; set; }

		public FixedAssetEntry(DateTime postingDate, decimal value, decimal valueChange, string name, string currency) : base(postingDate, value, valueChange)
		{
			Name = name;
			Currency = currency;
		}

	}
	public class BankAccountEntry : FinancialEntryBase
	{
		public string Description { get; set; }
		public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;

		public BankAccountEntry(DateTime postingDate, decimal value, decimal valueChange) : base(postingDate, value, valueChange)
		{
		}
	}
}
