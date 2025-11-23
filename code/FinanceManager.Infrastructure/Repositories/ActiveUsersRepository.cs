using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;
public class ActiveUsersRepository(AppDbContext context) : IActiveUsersRepository
{
    public async Task Add(int userId, DateOnly dateOnly)
    {
        if (await Get(userId, dateOnly) is not null) return;

        await context.ActiveUsers.AddAsync(new ActiveUser()
        {
            UserId = userId,
            LoginTime = dateOnly.ToDateTime(TimeOnly.MinValue),
        });

        await context.SaveChangesAsync();
    }

    public Task<ActiveUser?> Get(int userId, DateOnly dateOnly) =>
        context.ActiveUsers.FirstOrDefaultAsync(x => x.UserId == userId && x.LoginTime.Date == dateOnly.ToDateTime(TimeOnly.MinValue));

    public Task<int> GetActiveUsersCount(DateOnly dateOnly) =>
        context.ActiveUsers.CountAsync(x => x.LoginTime.Date == dateOnly.ToDateTime(TimeOnly.MinValue));

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