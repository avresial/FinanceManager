using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetUser(string login, string password);
    Task<User?> GetUser(int id);
    Task<bool> UpdatePassword(int userId, string password);
    Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel);
    Task<bool> AddUser(string login, string password, PricingLevel pricingLevel);
    Task<bool> RemoveUser(int userId);
}
