using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Services
{
    public interface IUserService
    {
        Task<bool> AddUser(string login, string password, PricingLevel pricingLevel);
        Task<User?> GetUser(int id);
    }
}
