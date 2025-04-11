using FinanceManager.Domain.Entities.Login;

namespace FinanceManager.Domain.Repositories;

public interface IUserRepository
{
    public Task<User?> GetUser(string login, string password);
    public Task<bool> AddUser(string login, string password);
    public Task<bool> RemoveUser(int userId);
}
