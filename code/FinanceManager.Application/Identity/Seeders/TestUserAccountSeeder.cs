using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Identity.Seeders;

public class TestUserAccountSeeder(IUserRepository userRepository, IConfiguration configuration,
    ILogger<TestUserAccountSeeder> logger) : ISeeder
{
    private const string _defaultTestUserName = "testuser@localhost";

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var existingTestUser = await userRepository.GetUser(_defaultTestUserName);
        if (existingTestUser is not null) return;

        // Demo/test credentials belong in configuration (Seeding:TestUserPassword) — e.g.
        // appsettings.Development.json or an environment variable — not in source code.
        // When unset (such as in production) the test account is simply not created.
        var password = configuration["Seeding:TestUserPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Skipping test user account seeding: 'Seeding:TestUserPassword' is not configured.");
            return;
        }

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(password);
        await userRepository.AddUser(_defaultTestUserName, encryptedPassword, PricingLevel.Free, UserRole.User);
    }
}