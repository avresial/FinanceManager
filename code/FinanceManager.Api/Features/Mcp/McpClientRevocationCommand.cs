using FinanceManager.Infrastructure.OAuth;

namespace FinanceManager.Api.Features.Mcp;

internal static class McpClientRevocationCommand
{
    private const string _argument = "--revoke-mcp-client";

    public static bool IsRequested(string[] args) => Array.IndexOf(args, _argument) >= 0;

    public static Task<int> RunAsync(string[] args, IServiceProvider services) =>
        RunAsync(
            args,
            async clientId =>
            {
                using var scope = services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<McpOAuthGrantRevoker>()
                    .RevokeClientAsync(clientId);
            },
            Console.Out,
            Console.Error);

    internal static async Task<int> RunAsync(
        string[] args,
        Func<string, Task<McpOAuthRevocationResult?>> revokeClient,
        TextWriter output,
        TextWriter error)
    {
        if (args.Length != 2 || args[0] != _argument || string.IsNullOrWhiteSpace(args[1]))
        {
            await error.WriteLineAsync("Usage: FinanceManager.Api --revoke-mcp-client <client-id>");
            return 2;
        }

        var result = await revokeClient(args[1]);
        if (result is null)
        {
            await error.WriteLineAsync($"MCP OAuth client '{args[1]}' was not found.");
            return 3;
        }

        await output.WriteLineAsync(
            $"Revoked {result.Authorizations} authorization(s) and {result.Tokens} token(s) for MCP OAuth client '{args[1]}'.");
        return 0;
    }
}