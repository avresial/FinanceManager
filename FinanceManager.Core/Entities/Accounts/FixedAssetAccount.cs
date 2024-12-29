namespace FinanceManager.Core.Entities.Accounts
{
    public class FixedAssetAccount : FinancialAccountBase<FixedAssetEntry>
    {
        public FixedAssetAccount(int id, string name) : base(id, name)
        {

        }
    }
}
