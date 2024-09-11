using FinanceManager.Core.Enums;

namespace FinanceManager.Core.Entities
{
	public class BankAccountEntry
	{
		public DateTime PostingDate { get; set; }
		public decimal Balance { get; set; }
		public decimal BalanceChange { get; set; }
		public string Description { get; set; }
		public ExpenseType ExpenseType { get; set; } = ExpenseType.Other;
	}
}
