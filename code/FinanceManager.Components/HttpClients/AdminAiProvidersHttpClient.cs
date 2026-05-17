using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class AdminAiProvidersHttpClient(HttpClient httpClient)
{
    public sealed record AiProviderDto(
        string ProviderName,
        string BaseUrl,
        bool HasApiKey,
        int RequestTimeoutSeconds,
        bool IsEnabled);

    public sealed record AiFallbackEntryDto(string ProviderName, string Model, int Order);

    public sealed record AiConfigurationResponse(
        List<AiProviderDto> Providers,
        List<AiFallbackEntryDto> FallbackEntries);

    public sealed record UpdateProviderRequest(
        string BaseUrl,
        string? ApiKey,
        int RequestTimeoutSeconds,
        bool IsEnabled);

    public sealed record UpdateFallbackRequest(
        List<AiFallbackEntryDto> Entries);

    public async Task<AiConfigurationResponse?> GetConfigurationAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<AiConfigurationResponse>(
            $"{httpClient.BaseAddress}api/admin/ai-providers", ct);

    public async Task UpdateProviderAsync(string providerName, UpdateProviderRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/{Uri.EscapeDataString(providerName)}", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateFallbackAsync(UpdateFallbackRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/fallback", request, ct);
        response.EnsureSuccessStatusCode();
    }
}
