using FinanceManager.Application.Providers;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;

namespace FinanceManager.Application.Services;
public class AdminAccountSeeder(IUserRepository loginRepository)
{
    private const string _defaultAdminUserName = "admin";
    private const string _defaultAdminPassword = "admin";
    private readonly IUserRepository _userRepository = loginRepository;

    public async Task Seed()
    {
        var existingAdmin = await _userRepository.GetUser(_defaultAdminUserName);
        if (existingAdmin is not null) return;

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(_defaultAdminPassword);
        var result = await _userRepository.AddUser(_defaultAdminUserName, encryptedPassword, PricingLevel.Free, UserRole.Admin);
    }
}
