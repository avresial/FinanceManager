using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.TransactionRules.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.TransactionRules;

internal static class Registration
{
    public static IServiceCollection AddTransactionRulesApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITransactionRuleEngineService, TransactionRuleEngineService>();
        services.AddScoped<ITransactionRuleService, TransactionRuleService>();

        return services;
    }
}