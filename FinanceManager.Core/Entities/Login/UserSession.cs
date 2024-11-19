namespace FinanceManager.Core.Entities.Login
{
    public class UserSession
    {
        public int UserId { get; set; }
        public required string UserName { get; set; }
    }
}
