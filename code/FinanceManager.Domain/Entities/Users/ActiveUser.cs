namespace FinanceManager.Domain.Entities.Users;

public class ActiveUser
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime LoginTime { get; set; }
}