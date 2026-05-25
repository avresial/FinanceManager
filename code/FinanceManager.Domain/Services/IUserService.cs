using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Services;

public interface IUserService
{
    event Action<User>? OnUserChangeEvent;

    Task<bool> AddUser(string login, string password, PricingLevel pricingLevel);
    Task<User?> GetUser(int userId);
    Task<RecordCapacity?> GetRecordCapacity(int userId);
    Task<bool> Delete(int userId);
    Task<bool> UpdatePassword(int userId, string newPassword);
    Task<bool> UpdatePricingPlan(int userId, PricingLevel newPricingLevel);
    Task<bool> UpdateRole(int userId, UserRole userRole);

}