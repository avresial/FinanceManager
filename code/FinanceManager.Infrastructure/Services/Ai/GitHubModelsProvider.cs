using FinanceManager.Application.Options;
using FinanceManager.Application.Services.Ai;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class GitHubModelsProvider(
    IOptions<GitHubModelsOptions> options,
    ILogger<GitHubModelsProvider> logger) : IAiProvider
{
    public async Task<string?> Get(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var model = options.Value.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            logger.LogWarning("Copilot SDK model is not configured.");
            return null;
        }

        var timeoutSeconds = options.Value.RequestTimeoutSeconds > 0
            ? options.Value.RequestTimeoutSeconds
            : 60;

        await using var client = new CopilotClient();
        var started = false;
        string? sessionid = null;
        try
        {
            await client.StartAsync(cancellationToken);
            started = true;

            var sessionConfig = new SessionConfig { Model = model };
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                sessionConfig.SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = systemPrompt
                };
            }

            await using var session = await client.CreateSessionAsync(sessionConfig, cancellationToken);
            sessionid = session.SessionId;

            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = userPrompt },
                TimeSpan.FromSeconds(timeoutSeconds),
                cancellationToken);
            return response?.Data?.Content;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Copilot SDK request timed out");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Copilot SDK request failed");
            return null;
        }
        finally
        {
            if (started)
            {
                try
                {
                    await client.StopAsync();

                    if (sessionid != null)
                        await client.DeleteSessionAsync(sessionid, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Copilot SDK shutdown failed");
                }
            }
        }
    }
}
