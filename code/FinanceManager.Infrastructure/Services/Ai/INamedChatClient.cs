using Microsoft.Extensions.AI;

namespace FinanceManager.Infrastructure.Services.Ai;

internal interface INamedChatClient : IChatClient
{
    string ProviderName { get; }
}
