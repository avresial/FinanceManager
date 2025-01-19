namespace FinanceManager.Domain.Entities.Accounts
{
    public class BasicAccountInformation
    {
        public int Id { get; set; }

        /// <summary>
        /// Owner
        /// </summary>
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public virtual DateTime? Start { get; protected set; }
        public virtual DateTime? End { get; protected set; }
    }
}
