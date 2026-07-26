using FinanceManager.Application.Shared.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure.Shared.Ai;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddAI(this IServiceCollection services)
    {
        services.AddSingleton<IAiConfigurationService, AiConfigurationService>();

        services.AddScoped<INamedChatClient, LmStudioChatClient>();
        services.AddScoped<INamedChatClient, OpenRouterChatClient>();
        services.AddScoped<INamedChatClient, CopilotChatClient>();
        services.AddScoped<INamedChatClient, OllamaChatClient>();
        services.AddScoped<IChatClient, FallbackChatClient>();

        return services;
    }
}