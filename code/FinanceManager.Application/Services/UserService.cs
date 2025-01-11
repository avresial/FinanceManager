using FinanceManager.Application.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services
{
    public class UserService(ILoginRepository loginRepository) : IUserService
    {
        private readonly ILoginRepository _loginRepository = loginRepository;

        public async Task<bool> AddUser(string login, string password)
        {
            return await _loginRepository.AddUser(login, PasswordEncryptionProvider.EncryptPassword(password));
        }
    }
}
