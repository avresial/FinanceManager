using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.Extensions.Configuration;

namespace FinanceManager.Infrastructure.Repositories;

internal class UserInMemoryRepository : IUserRepository
{
    private readonly Dictionary<string, UserDto> _users = [];

    public UserInMemoryRepository(IConfiguration configuration)
    {
        var defaultUserLogin = configuration["DefaultUser:Login"];
        var defaultUserPassword = configuration["DefaultUser:Password"];

        if (!string.IsNullOrEmpty(defaultUserLogin) && !string.IsNullOrEmpty(defaultUserPassword))
        {
            _users[defaultUserLogin] = new UserDto
            {
                Login = defaultUserLogin,
                Password = defaultUserPassword,
                Id = 0
            };
        }
    }
    public async Task<bool> AddUser(string login, string password)
    {
        if (_users.ContainsKey(login)) return await Task.FromResult(false);

        _users.Add(login, new UserDto()
        {
            Login = login,
            Password = password,
            Id = GenerateNewId()
        });

        return await Task.FromResult(true);
    }

    public async Task<User?> GetUser(string login, string password)
    {
        if (!_users.ContainsKey(login)) return null;
        if (password != _users[login].Password) return null;

        var result = new User() { Login = _users[login].Login, Id = _users[login].Id };
        return await Task.FromResult(result);
    }

    public async Task<bool> RemoveUser(int userId)
    {
        var userToRemove = _users.Values.FirstOrDefault(x => x.Id == userId);
        if (userToRemove is null) return await Task.FromResult(false);

        _users.Remove(userToRemove.Login);
        return await Task.FromResult(true);
    }

    private int GenerateNewId()
    {
        return _users.Values.Count != 0 ? _users.Values.Max(u => u.Id) + 1 : 1;
    }
}
