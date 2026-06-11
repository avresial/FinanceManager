using FinanceManager.Application.Insights.Diversification;
using FinanceManager.Application.Insights.Generation;
using FinanceManager.Application.Insights.Seeders;
using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Insights;

internal static class Registration
{
    public static IServiceCollection AddInsightsApplication(this IServiceCollection services)
    {
        services.AddScoped<IFinancialInsightsAiGenerator, FinancialInsightsAiGenerator>()
                .AddScoped<InsightsContextBuilder>()
                .AddSingleton<InsightsResponseParser>()
                .AddSingleton<FinancialInsightNormalizer>()
                .AddScoped<IDiversificationService, DiversificationService>()
                .AddScoped<FinancialInsightsSeeder>();
        // .AddScoped<ISeeder, FinancialInsightsSeeder>()

        return services;
    }
}