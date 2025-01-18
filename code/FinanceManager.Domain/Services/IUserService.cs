namespace FinanceManager.Domain.Services
{
    public interface IUserService
    {
        Task<bool> AddUser(string login, string password);
    }
}
