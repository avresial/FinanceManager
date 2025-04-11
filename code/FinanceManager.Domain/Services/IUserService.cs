using FinanceManager.Domain.Entities.Login;

namespace FinanceManager.Domain.Services
{
    public interface IUserService
    {
        Task<bool> AddUser(string login, string password);
        Task<User?> GetUser(int id);
    }
}
