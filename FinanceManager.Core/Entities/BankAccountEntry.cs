namespace FinanceManager.Core.Entities
{
	public class BankAccountEntry
	{
		public DateTime PostingDate { get; set; }
		public double Balance { get; set; }
		public double BalanceChange { get; set; }
		public string SenderName { get; set; }
	}
}
