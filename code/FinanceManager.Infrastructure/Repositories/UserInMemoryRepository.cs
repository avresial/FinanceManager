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
                PricingLevel = PricingLevel.Basic
            };
        }

    }
    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole)
    {
        if (_users.ContainsKey(login)) return await Task.FromResult(false);

        _users.Add(login, new UserDto()
        {
            Login = login,
            Password = password,
            Id = GenerateNewId(),
            PricingLevel = pricingLevel,
            UserRole = userRole
        });

        return await Task.FromResult(true);
    }

    public async Task<User?> GetUser(string login, string password)
    {
        if (!_users.ContainsKey(login)) return null;
        if (password != _users[login].Password) return null;

        var result = new User() { Login = _users[login].Login, Id = _users[login].Id, PricingLevel = _users[login].PricingLevel, UserRole = _users[login].UserRole };
        return await Task.FromResult(result);
    }
    public async Task<User?> GetUser(string login)
    {
        if (!_users.ContainsKey(login)) return null;

        var result = new User() { Login = _users[login].Login, Id = _users[login].Id, PricingLevel = _users[login].PricingLevel, UserRole = _users[login].UserRole };
        return await Task.FromResult(result);
    }

    public async Task<User?> GetUser(int id)
    {
        var user = _users.Values.FirstOrDefault(x => x.Id == id);
        if (user is null) return null;

        return await Task.FromResult(new User() { Login = user.Login, Id = user.Id, PricingLevel = user.PricingLevel, UserRole = user.UserRole });
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
