using FinanceManager.Core.Entities.Login;

namespace FinanceManager.Core.Repositories
{
    public interface ILoginRepository
    {
        public Task<User?> GetUser(string login, string password);
        public Task<bool> AddUser(string login, string password);
        public Task<bool> RemoveUser(int userId);
    }
}
