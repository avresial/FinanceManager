using FinanceManager.Infrastructure.Repositories;

namespace FinanceManager.Domain.Repositories;

public interface IActiveUsersRepository
{
    Task Add(int userId, DateOnly dateOnly);
    Task<ActiveUser?> Get(int userId, DateOnly dateOnly);
    Task<int> GetActiveUsersCount(DateOnly dateOnly);
    Task<IEnumerable<(DateOnly, int)>> GetActiveUsersCount(DateOnly dateStart, DateOnly dateEnd);
}