using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class AdminAiProvidersHttpClient(HttpClient httpClient)
{
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
