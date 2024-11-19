using FinanceManager.Core.Entities.Login;

namespace FinanceManager.Core.Repositories
{
    public interface ILoginRepository
    {
        public User GetUser(string login, string password);
        public bool AddUser(string login, string password);
    }
}
