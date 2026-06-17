using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;

namespace FinanceManager.Application.Identity.Seeders;

public class AdminAccountSeeder(IUserRepository userRepository) : ISeeder
{
    private const string _defaultAdminUserName = "admin@localhost";
    private const string _defaultAdminPassword = "Admin1234";

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var existingAdmin = await userRepository.GetUser(_defaultAdminUserName);
        if (existingAdmin is not null) return;

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(_defaultAdminPassword);
        await userRepository.AddUser(_defaultAdminUserName, encryptedPassword, PricingLevel.Free, UserRole.Admin);
    }
}