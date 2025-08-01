﻿using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Components;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddUIComponents(this IServiceCollection services)
    {
        services.AddScoped<ILoginService, LoginService>()
                .AddScoped<StockPriceHttpContext>()
                .AddScoped<AccountDataSynchronizationService>()
                .AddScoped<IUserService, UserService>()
                .AddScoped<BankAccountService>()
                .AddScoped<AdministrationUsersService>()
                .AddScoped<StockAccountService>()
                .AddScoped<IFinancialAccountService, FinancialAccountService>()
                .AddScoped<IMoneyFlowService, MoneyFlowService>()
                .AddScoped<ILiabilitiesService, LiabilitiesService>()
                .AddScoped<NewVisitorsService>()
                .AddScoped<DuplicateEntryResolverService>()

                ;
        return services;
    }
}
