using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Repositories
{
    internal class UserInMemoryRepository : IUserRepository
    {
        private readonly Dictionary<string, UserDto> _users = new Dictionary<string, UserDto>();

        public async Task<bool> AddUser(string login, string password)
        {
            if (_users.ContainsKey(login)) return await Task.FromResult(false);

            _users.Add(login, new UserDto()
            {
                Login = login,
                Password = password,
                Id = _users.Count + 1
            });

            return await Task.FromResult(true);
        }

        public async Task<User?> GetUser(string login, string password)
        {
            if (!_users.ContainsKey(login)) return null;

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
    }
}
