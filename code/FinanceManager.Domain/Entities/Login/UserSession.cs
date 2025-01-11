namespace FinanceManager.Domain.Entities.Login
{
    public class UserSession
    {
        public int UserId { get; set; }
        public required string UserName { get; set; }
        public required string Password { get; set; }
    }
}
