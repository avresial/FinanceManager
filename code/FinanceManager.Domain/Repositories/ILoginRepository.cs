using FinanceManager.Domain.Entities.Login;

namespace FinanceManager.Domain.Repositories
{
    public interface ILoginRepository
    {
        public Task<User?> GetUser(string login, string password);
        public Task<bool> AddUser(string login, string password);
        public Task<bool> RemoveUser(int userId);
    }
}
