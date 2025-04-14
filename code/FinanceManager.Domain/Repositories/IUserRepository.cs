using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetUser(string login, string password);
    Task<User?> GetUser(int id);
    Task<bool> AddUser(string login, string password, PricingLevel pricingLevel);
    Task<bool> RemoveUser(int userId);
}
