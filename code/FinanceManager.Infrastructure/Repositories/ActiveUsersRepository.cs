using FinanceManager.Domain.Repositories;

namespace FinanceManager.Infrastructure.Repositories;
public class ActiveUsersRepository : IActiveUsersRepository
{
    public Task Add(int userId, DateOnly dateOnly)
    {
        throw new NotImplementedException();
    }

    public Task Get(int userId, DateOnly dateOnly)
    {
        throw new NotImplementedException();
    }

    public Task<int> GetActiveUsersCount(DateOnly dateOnly)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<(DateOnly, int)>> GetActiveUsersCount(DateOnly dateStart, DateOnly dateEnd)
    {
        throw new NotImplementedException();
    }
}
