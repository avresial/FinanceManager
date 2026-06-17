using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;

namespace FinanceManager.Application.Identity.Seeders;

public class TestUserAccountSeeder(IUserRepository userRepository) : ISeeder
{
    private const string _defaultTestUserName = "testuser";
    private const string _defaultTestUserPassword = "testuser";

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var existingTestUser = await userRepository.GetUser(_defaultTestUserName);
        if (existingTestUser is not null) return;

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(_defaultTestUserPassword);
        await userRepository.AddUser(_defaultTestUserName, encryptedPassword, PricingLevel.Free, UserRole.User);
    }
}