using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole)
    {
        context.Add(new UserDto
        {
            Login = login,
            Password = password,
            PricingLevel = pricingLevel,
            UserRole = userRole,
            CreationDate = DateTime.UtcNow,
        });

        await context.SaveChangesAsync();

        return true;
    }
    public async Task<User?> GetUser(string login, string password)
    {
        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null || userDto.Password != password) return null;

        return userDto.ToUser();
    }
    public async Task<User?> GetUser(string login)
    {
        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null) return null;

        return userDto.ToUser();
    }
    public async Task<User?> GetUser(int id)
    {
        var userDto = await context.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (userDto is null) return null;

        return userDto.ToUser();
    }
    public IAsyncEnumerable<User> GetUsers(int recordIndex, int recordsCount) => context.Users
        .Skip(recordIndex)
        .Take(recordsCount)
        .Select(x => x.ToUser())
        .ToAsyncEnumerable();

    public IAsyncEnumerable<User> GetUsers(DateTime startDate, DateTime endDate) => context.Users
        .Where(x => x.CreationDate >= startDate && x.CreationDate <= endDate)
        .Select(x => x.ToUser())
        .ToAsyncEnumerable();

    public Task<int> GetUsersCount() => context.Users.CountAsync();

    public async Task<bool> RemoveUser(int userId)
    {
        var userToRemove = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (userToRemove is null) return await Task.FromResult(false);

        context.Remove(userToRemove);

        await context.SaveChangesAsync();
        return await Task.FromResult(true);
    }
    public async Task<bool> UpdatePassword(int userId, string password)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return await Task.FromResult(false);

        user.Password = password;
        context.Update(user);
        await context.SaveChangesAsync();

        return await Task.FromResult(true);
    }
    public async Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return await Task.FromResult(false);

        user.PricingLevel = pricingLevel;
        context.Update(user);
        await context.SaveChangesAsync();

        return await Task.FromResult(true);
    }
}