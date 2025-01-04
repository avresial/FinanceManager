namespace FinanceManager.Core.Entities.Accounts
{
    public class FinancialAccountBase
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public virtual DateTime? Start { get; protected set; }
        public virtual DateTime? End { get; protected set; }
    }
}
