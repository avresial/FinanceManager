using FinanceManager.Application.Providers;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;

namespace FinanceManager.Application.Services;
public class AdminAccountSeeder(IUserRepository loginRepository)
{
    const string defaultAdminUserName = "admin";
    const string defaultAdminPassword = "admin";
    private readonly IUserRepository _userRepository = loginRepository;

    public async Task Seed()
    {
        var existingAdmin = await _userRepository.GetUser(defaultAdminUserName);
        if (existingAdmin is not null) return;

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(defaultAdminPassword);
        var result = await _userRepository.AddUser(defaultAdminUserName, encryptedPassword, Domain.Enums.PricingLevel.Free, UserRole.Admin);
    }
}
