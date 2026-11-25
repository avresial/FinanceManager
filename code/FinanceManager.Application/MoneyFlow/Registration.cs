using FinanceManager.Application.MoneyFlow.Assets;
using FinanceManager.Application.MoneyFlow.Balances;
using FinanceManager.Application.MoneyFlow.CashFlowForecast;
using FinanceManager.Application.MoneyFlow.InvestmentPaycheck;
using FinanceManager.Application.MoneyFlow.InvestmentRate;
using FinanceManager.Application.MoneyFlow.LabelsValue;
using FinanceManager.Application.MoneyFlow.NetWorth;
using FinanceManager.Application.MoneyFlow.SavingsRate;
using FinanceManager.Application.MoneyFlow.Spending;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.MoneyFlow;

internal static class Registration
{
    public static IServiceCollection AddMoneyFlowApplication(this IServiceCollection services)
    {
        services.AddScoped<IBalanceServiceTyped, CurrencyBalanceService>()
                .AddScoped<IBalanceServiceTyped, BondBalanceService>()
                .AddScoped<IBalanceServiceTyped, InvestmentBalanceService>()
                .AddScoped<IBalanceService, BalanceService>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceCurrency>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceBond>()
                .AddScoped<IAssetsServiceTyped, AssetsServiceInvestment>()
                .AddScoped<IInvestmentAppreciationService, AssetsServiceInvestment>()
                .AddScoped<IAssetsService, AssetsService>()
                .AddScoped<INetWorthService, NetWorthService>()
                .AddScoped<ICashFlowForecastService, CashFlowForecastService>()
                .AddScoped<ILabelsValueService, LabelsValueService>()
                .AddScoped<IInvestmentRateService, InvestmentRateService>()
                .AddScoped<ISavingsRateService, SavingsRateService>()
                .AddScoped<ILiabilitiesService, LiabilitiesService>()
                .AddScoped<IUnconvertedCurrencyService, UnconvertedCurrencyService>()
                .AddScoped<IInvestmentPaycheckEstimatorService, InvestmentPaycheckEstimatorService>()
                .AddScoped<IEssentialSpendingServiceTyped, CurrencyEssentialSpendingService>()
                .AddScoped<IEssentialSpendingService, EssentialSpendingService>()
                .AddScoped<IExpenseDistributionServiceTyped, CurrencyExpenseDistributionService>()
                .AddScoped<IExpenseDistributionService, ExpenseDistributionService>();

        return services;
    }
}