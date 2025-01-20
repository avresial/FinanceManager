namespace FinanceManager.Domain.Entities.Accounts
{
    public class FixedAssetAccount : FinancialAccountBase<FixedAssetEntry>
    {
        public FixedAssetAccount(int userId, int id, string name) : base(userId, id, name)
        {

        }
    }
}
