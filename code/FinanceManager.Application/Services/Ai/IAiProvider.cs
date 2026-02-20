namespace FinanceManager.Application.Services.Ai;

public interface IAiProvider
{
    Task<string?> Get(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
