using FinanceManager.Application.MoneyFlow.InvestmentPaycheck;
using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Application.MoneyFlow.Spending;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.MoneyFlow;

internal static class Registration
{
    public static IServiceCollection AddMoneyFlowApplication(this IServiceCollection services)
    {
        services.AddScoped<IMoneyFlowService, MoneyFlowService>()
                .AddScoped<ILiabilitiesService, LiabilitiesService>()
                .AddScoped<IInvestmentPaycheckEstimatorService, InvestmentPaycheckEstimatorService>()
                .AddScoped<IEssentialSpendingServiceTyped, CurrencyEssentialSpendingService>()
                .AddScoped<IEssentialSpendingService, EssentialSpendingService>()
                .AddScoped<IExpenseDistributionServiceTyped, CurrencyExpenseDistributionService>()
                .AddScoped<IExpenseDistributionService, ExpenseDistributionService>();

        return services;
    }
}