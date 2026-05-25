using FinanceManager.Domain.Entities.Users;
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
    IAsyncEnumerable<int> GetUsersIds(int recordIndex, int recordsCount);
    Task<bool> UpdatePassword(int userId, string password);
    Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel);
    Task<bool> AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole);

    /// <summary>
    /// Inserts a user with an explicit id. Intended for ephemeral guest sandboxes where the id is chosen by the
    /// session store ahead of any persistence and cannot be reassigned by an identity column.
    /// </summary>
    Task<bool> AddUserWithId(int userId, string login, string password, PricingLevel pricingLevel, UserRole userRole);

    Task<bool> RemoveUser(int userId);
}