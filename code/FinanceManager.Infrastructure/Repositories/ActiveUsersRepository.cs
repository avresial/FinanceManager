using FinanceManager.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;
public class ActiveUsersRepository(ActiveUsersContext activeUsersContext) : IActiveUsersRepository
{
    private readonly ActiveUsersContext _activeUsersContext = activeUsersContext;

    public Task Add(int userId, DateOnly dateOnly)
    {
        throw new NotImplementedException();
    }

    public Task Get(int userId, DateOnly dateOnly)
    {
        throw new NotImplementedException();
    }

    public async Task<int> GetActiveUsersCount(DateOnly dateOnly)
    {
        return await _activeUsersContext.ActiveUsers.CountAsync(x => x.CreatedAt.Date == dateOnly.ToDateTime(TimeOnly.MinValue));
    }

    public async Task<IEnumerable<(DateOnly, int)>> GetActiveUsersCount(DateOnly dateStart, DateOnly dateEnd)
    {
        List<(DateOnly, int)> results = [];
        for (DateTime i = dateStart.ToDateTime(new TimeOnly()); i <= dateEnd.ToDateTime(new TimeOnly()); i = i.AddDays(1))
        {
            var activeUsers = await GetActiveUsersCount(DateOnly.FromDateTime(i));
            results.Add((DateOnly.FromDateTime(i), activeUsers));
        }
        return results;
    }

}


public class ActiveUsersContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase(databaseName: "ActiveUsersDb");
    }
    public DbSet<ActiveUser> ActiveUsers { get; set; }
}

public class ActiveUser
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}
