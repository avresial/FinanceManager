using Microsoft.Extensions.AI;

namespace FinanceManager.Infrastructure.Shared.Ai;

internal interface INamedChatClient : IChatClient
{
    string ProviderName { get; }
}