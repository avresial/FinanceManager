namespace FinanceManager.Domain.Entities.Accounts
{
    public class FixedAssetEntry : FinancialEntryBase
    {
        public string Name { get; set; }
        public string Currency { get; set; }

        public FixedAssetEntry(int accountId, int entryId, DateTime postingDate, decimal value, decimal valueChange, string name, string currency)
            : base(accountId, entryId, postingDate, value, valueChange)
        {
            Name = name;
            Currency = currency;
        }

    }
}
