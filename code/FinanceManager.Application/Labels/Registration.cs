using FinanceManager.Application.Labels.RecurringTransactions;
using FinanceManager.Application.Labels.Seeders;
using FinanceManager.Application.Labels.Setter;
using FinanceManager.Application.Labels.Suggestions;
using FinanceManager.Application.Shared.Seeders;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Services;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Application.Labels;

internal static class Registration
{
    public static IServiceCollection AddLabelsApplication(this IServiceCollection services)
    {
        services.AddScoped<ILabelSetterAiService, LabelSetterAiService>()
                .AddSingleton<LabelAssignmentResponseParser>()
                .AddSingleton<LabelAssignmentValidator>()
                .AddScoped<ILabelSuggestionAiService, LabelSuggestionAiService>()
                .AddScoped<IRecurringTransactionDetectorService, RecurringTransactionDetectorService>()
                .AddScoped<FinancialLabelSeeder>()
                .AddScoped<ISeeder, FinancialLabelSeeder>(sp => sp.GetRequiredService<FinancialLabelSeeder>());

        return services;
    }
}