using FinanceManager.Application.MoneyFlow.InvestmentPaycheck;
using FinanceManager.Application.MoneyFlow.InvestmentRate;
using FinanceManager.Application.MoneyFlow.LabelsValue;
using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Application.MoneyFlow.Spending;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.MoneyFlow;

internal static class Registration
{
    public static IServiceCollection AddMoneyFlowApplication(this IServiceCollection services)
    {
        services.AddScoped<INetWorthService, NetWorthService>()
                .AddScoped<ILabelsValueService, LabelsValueService>()
                .AddScoped<IInvestmentRateService, InvestmentRateService>()
                .AddScoped<ILiabilitiesService, LiabilitiesService>()
                .AddScoped<IInvestmentPaycheckEstimatorService, InvestmentPaycheckEstimatorService>()
                .AddScoped<IEssentialSpendingServiceTyped, CurrencyEssentialSpendingService>()
                .AddScoped<IEssentialSpendingService, EssentialSpendingService>()
                .AddScoped<IExpenseDistributionServiceTyped, CurrencyExpenseDistributionService>()
                .AddScoped<IExpenseDistributionService, ExpenseDistributionService>();

        return services;
    }
}