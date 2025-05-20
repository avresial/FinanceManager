using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Infrastructure.Repositories;

public class UserInMemoryRepository : IUserRepository
{
    private readonly Dictionary<string, UserDto> _users = [];

    public UserInMemoryRepository() { }
    public UserInMemoryRepository(IConfiguration configuration)
    {
        if (configuration is null) return;
        var defaultUserLogin = configuration["DefaultUser:Login"];
        var defaultUserPassword = configuration["DefaultUser:Password"];

        if (!string.IsNullOrEmpty(defaultUserLogin) && !string.IsNullOrEmpty(defaultUserPassword))
        {
            _users[defaultUserLogin] = new UserDto
            {
                Login = defaultUserLogin,
                Password = defaultUserPassword,
                Id = 0,
                PricingLevel = PricingLevel.Basic,
                CreationDate = DateTime.Now,
            };
        }

    }
    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole)
    {
        lock (_users)
        {
            if (_users.ContainsKey(login)) return false;

            _users.Add(login, new UserDto()
            {
                Login = login,
                Password = password,
                Id = GenerateNewId(),
                PricingLevel = pricingLevel,
                UserRole = userRole,
                CreationDate = DateTime.Now,
            });
        }

        return await Task.FromResult(true);
    }

    public async Task<User?> GetUser(string login, string password)
    {
        if (!_users.ContainsKey(login)) return null;
        if (password != _users[login].Password) return null;

        var result = new User() { Login = _users[login].Login, UserId = _users[login].Id, PricingLevel = _users[login].PricingLevel, UserRole = _users[login].UserRole, CreationDate = _users[login].CreationDate };
        return await Task.FromResult(result);
    }
    public async Task<User?> GetUser(string login)
    {
        if (!_users.ContainsKey(login)) return null;

        var result = new User() { Login = _users[login].Login, UserId = _users[login].Id, PricingLevel = _users[login].PricingLevel, UserRole = _users[login].UserRole, CreationDate = _users[login].CreationDate };
        return await Task.FromResult(result);
    }

    public async Task<User?> GetUser(int id)
    {
        var user = _users.Values.FirstOrDefault(x => x.Id == id);
        if (user is null) return null;

        return await Task.FromResult(new User() { Login = user.Login, UserId = user.Id, PricingLevel = user.PricingLevel, UserRole = user.UserRole, CreationDate = user.CreationDate });
    }

    public async Task<IEnumerable<User>> GetUsers(int recordIndex, int recordsCount)
    {
        List<User> result = [];
        lock (_users)
        {
            result = _users.Values.Skip(recordIndex).Take(recordsCount)
                .Select(x => new User() { Login = x.Login, UserId = x.Id, PricingLevel = x.PricingLevel, UserRole = x.UserRole, CreationDate = x.CreationDate })
                .ToList();
        }

        return await Task.FromResult(result);

    }

    public async Task<IEnumerable<User>> GetUsers(DateTime startDate, DateTime endDate)
    {
        List<User> result = [];
        lock (_users)
        {
            result = _users.Values
                .Where(x => x.CreationDate >= startDate && x.CreationDate <= endDate)
                .Select(x => new User() { Login = x.Login, UserId = x.Id, PricingLevel = x.PricingLevel, UserRole = x.UserRole, CreationDate = x.CreationDate })
                .ToList();
        }
        return await Task.FromResult(result);
    }

    public async Task<int> GetUsersCount()
    {
        return await Task.FromResult(_users.Count);
    }

    public async Task<bool> RemoveUser(int userId)
    {
        var userToRemove = _users.Values.FirstOrDefault(x => x.Id == userId);
        if (userToRemove is null) return await Task.FromResult(false);

        _users.Remove(userToRemove.Login);
        return await Task.FromResult(true);
    }

    public async Task<bool> UpdatePassword(int userId, string password)
    {
        var user = _users.Values.FirstOrDefault(x => x.Id == userId);
        if (user is null) return false;

        lock (_users) user.Password = password;

        return await Task.FromResult(true);
    }

    public async Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel)
    {
        var user = _users.Values.FirstOrDefault(x => x.Id == userId);
        if (user is null) return false;

        lock (_users) user.PricingLevel = pricingLevel;

        return await Task.FromResult(true);
    }

    private int GenerateNewId()
    {
        return _users.Values.Count != 0 ? _users.Values.Max(u => u.Id) + 1 : 1;
    }
}
