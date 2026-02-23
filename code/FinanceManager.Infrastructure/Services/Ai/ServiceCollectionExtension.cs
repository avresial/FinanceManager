using FinanceManager.Application.Services.Stocks;
using FinanceManager.Infrastructure.Services.Stocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure.Services.Ai;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddAI(this IServiceCollection services)
    {
        services.AddHttpClient<IAlphaVantageClient, AlphaVantageClient>();

        services.AddScoped<INamedChatClient, OpenRouterChatClient>();
        services.AddScoped<INamedChatClient, CopilotChatClient>();
        services.AddScoped<INamedChatClient, OllamaChatClient>();
        services.AddScoped<IChatClient, FallbackChatClient>();
        ;

        return services;
    }
}