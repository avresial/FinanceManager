﻿using FinanceManager.Application.Providers;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;

namespace FinanceManager.Application.Services;
public class AdminAccountSeeder(IUserRepository userRepository)
{
    private const string _defaultAdminUserName = "admin";
    private const string _defaultAdminPassword = "admin";

    public async Task Seed()
    {
        var existingAdmin = await userRepository.GetUser(_defaultAdminUserName);
        if (existingAdmin is not null) return;

        var encryptedPassword = PasswordEncryptionProvider.EncryptPassword(_defaultAdminPassword);
        await userRepository.AddUser(_defaultAdminUserName, encryptedPassword, PricingLevel.Free, UserRole.Admin);
    }
}
