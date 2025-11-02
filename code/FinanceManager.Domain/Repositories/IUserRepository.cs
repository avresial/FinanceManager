using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Repositories;

public interface IUserRepository
{
    Task<int> GetUsersCount();
    Task<User?> GetUser(string login, string password);
    Task<User?> GetUser(string login);
    Task<User?> GetUser(int id);
    IAsyncEnumerable<User> GetUsers(DateTime startDate, DateTime endDate);
    IAsyncEnumerable<User> GetUsers(int recordIndex, int recordsCount);
    Task<bool> UpdatePassword(int userId, string password);
    Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel);
    Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole);
    Task<bool> RemoveUser(int userId);
}
