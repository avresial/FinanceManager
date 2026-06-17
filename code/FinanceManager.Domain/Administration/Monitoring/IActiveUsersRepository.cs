using FinanceManager.Domain.Identity.Entities;

namespace FinanceManager.Domain.Administration.Monitoring;

public interface IActiveUsersRepository
{
    Task Add(int userId, DateOnly dateOnly);
    Task<ActiveUser?> Get(int userId, DateOnly dateOnly);
    Task<int> GetActiveUsersCount(DateOnly dateOnly);
    Task<IEnumerable<(DateOnly, int)>> GetActiveUsersCount(DateOnly dateStart, DateOnly dateEnd);
}