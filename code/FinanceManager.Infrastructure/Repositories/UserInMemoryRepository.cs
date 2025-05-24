using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Infrastructure.Repositories;

public class UserInMemoryRepository : IUserRepository
{
    private readonly UsersContext _usersContext;

    public UserInMemoryRepository(UsersContext usersContext, IConfiguration configuration)
    {
        _usersContext = usersContext;

        if (configuration is null) return;
        var defaultUserLogin = configuration["DefaultUser:Login"];
        var defaultUserPassword = configuration["DefaultUser:Password"];

        if (!string.IsNullOrEmpty(defaultUserLogin) && !string.IsNullOrEmpty(defaultUserPassword))
        {
            _usersContext.Add(new UserDto
            {
                Login = defaultUserLogin,
                Password = defaultUserPassword,
                Id = 0,
                PricingLevel = PricingLevel.Basic,
                CreationDate = DateTime.Now,
            });

            _usersContext.SaveChanges();


        }
    }

    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole)
    {
        _usersContext.Add(new UserDto
        {
            Login = login,
            Password = password,
            PricingLevel = pricingLevel,
            UserRole = userRole,
            CreationDate = DateTime.Now,
        });

        await _usersContext.SaveChangesAsync();

        return true;
    }
    public async Task<User?> GetUser(string login, string password)
    {
        var userDto = await _usersContext.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null || userDto.Password != password) return null;

        return userDto.ToUser();
    }
    public async Task<User?> GetUser(string login)
    {
        UserDto? userDto = await _usersContext.Users.FirstOrDefaultAsync(x => x.Login == login);
        if (userDto is null) return null;

        return userDto.ToUser();
    }
    public async Task<User?> GetUser(int id)
    {
        UserDto? userDto = await _usersContext.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (userDto is null) return null;

        return userDto.ToUser();
    }
    public async Task<IEnumerable<User>> GetUsers(int recordIndex, int recordsCount)
    {
        return await _usersContext.Users.Skip(recordIndex).Take(recordsCount)
                .Select(x => x.ToUser())
                .ToListAsync();
    }
    public async Task<IEnumerable<User>> GetUsers(DateTime startDate, DateTime endDate)
    {
        return await _usersContext.Users
            .Where(x => x.CreationDate >= startDate && x.CreationDate <= endDate)
            .Select(x => x.ToUser())
            .ToListAsync();
    }
    public async Task<int> GetUsersCount()
    {
        return await _usersContext.Users.CountAsync();
    }
    public async Task<bool> RemoveUser(int userId)
    {
        var userToRemove = await _usersContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (userToRemove is null) return await Task.FromResult(false);

        _usersContext.Remove(userToRemove);

        await _usersContext.SaveChangesAsync();
        return await Task.FromResult(true);
    }
    public async Task<bool> UpdatePassword(int userId, string password)
    {
        var user = await _usersContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return await Task.FromResult(false);

        user.Password = password;
        _usersContext.Update(user);
        await _usersContext.SaveChangesAsync();

        return await Task.FromResult(true);
    }
    public async Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel)
    {
        var user = await _usersContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null) return await Task.FromResult(false);

        user.PricingLevel = pricingLevel;
        _usersContext.Update(user);
        await _usersContext.SaveChangesAsync();

        return await Task.FromResult(true);
    }
}
