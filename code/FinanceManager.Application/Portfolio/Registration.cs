using FinanceManager.Application.Portfolio.Dividends;
using FinanceManager.Application.Portfolio.FeeDrag;
using FinanceManager.Application.Portfolio.Performance;
using FinanceManager.Application.Portfolio.Valuation;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Portfolio.FeeDrag.Services;
using FinanceManager.Domain.Portfolio.Valuation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Portfolio;

internal static class Registration
{
    public static IServiceCollection AddPortfolioApplication(this IServiceCollection services)
    {
        services.AddScoped<IInvestmentValuationService, InvestmentValuationService>()
                .AddScoped<IInvestmentCapitalSeriesService, InvestmentCapitalSeriesService>()
                .AddScoped<IInvestmentBenchmarkService, InvestmentBenchmarkService>()
                .AddScoped<IMoneyWeightedReturnService, PortfolioMoneyWeightedReturnService>()
                .AddScoped<ITimeWeightedReturnService, PortfolioTimeWeightedReturnService>()
                .AddScoped<IPortfolioReturnAttributionService, PortfolioReturnAttributionService>()
                .AddScoped<IInvestmentTransactionValuationService, InvestmentTransactionValuationService>()
                .AddScoped<IFeeDragService, FeeDragService>()
                .AddScoped<DividendPaymentService>();

        return services;
    }
}