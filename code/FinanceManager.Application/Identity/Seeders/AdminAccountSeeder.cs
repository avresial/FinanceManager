using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Application.Identity.Seeders;

public class AdminAccountSeeder(IUserRepository userRepository, IConfiguration configuration,
    ILogger<AdminAccountSeeder> logger) : ISeeder
{
    private const string _defaultAdminUserName = "admin@localhost";

    public async Task Seed(CancellationToken cancellationToken = default)
    {
        var existingAdmin = await userRepository.GetUser(_defaultAdminUserName);
        if (existingAdmin is not null) return;

        // The default admin password is supplied via configuration (Seeding:AdminPassword)
        // — e.g. appsettings.Development.json, user secrets, or an environment variable —
        // so no credential is hard-coded. When it is not configured, skip seeding rather
        // than create an account with a predictable password.
        var password = configuration["Seeding:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Skipping admin account seeding: 'Seeding:AdminPassword' is not configured.");
            return;
        }

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(password);
        await userRepository.AddUser(_defaultAdminUserName, encryptedPassword, PricingLevel.Free, UserRole.Admin);
    }
}