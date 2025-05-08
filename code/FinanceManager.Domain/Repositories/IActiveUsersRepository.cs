namespace FinanceManager.Domain.Repositories;

public interface IActiveUsersRepository
{
    Task Add(int userId, DateOnly dateOnly);
    Task Get(int userId, DateOnly dateOnly);
    Task<int> GetActiveUsersCount(DateOnly dateOnly);
    Task<IEnumerable<(DateOnly, int)>> GetActiveUsersCount(DateOnly dateStart, DateOnly dateEnd);
}