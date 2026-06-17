namespace FinanceManager.Domain.Administration.Monitoring;

public class ActiveUser
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime LoginTime { get; set; }
}