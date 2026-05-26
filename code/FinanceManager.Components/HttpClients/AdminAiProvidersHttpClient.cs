using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class AdminAiProvidersHttpClient(HttpClient httpClient)
{
    public async Task<AiConfigurationResponse?> GetConfigurationAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<AiConfigurationResponse>(
            $"{httpClient.BaseAddress}api/admin/ai-providers", ct);

    public async Task AddProviderAsync(AddProviderRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateProviderAsync(string providerName, UpdateProviderRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/{Uri.EscapeDataString(providerName)}", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteProviderAsync(string providerName, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/{Uri.EscapeDataString(providerName)}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task AddModelAsync(string providerName, AddModelRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/{Uri.EscapeDataString(providerName)}/models", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateModelAsync(string providerName, int modelId, UpdateModelRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/{Uri.EscapeDataString(providerName)}/models/{modelId}", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteModelAsync(string providerName, int modelId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/{Uri.EscapeDataString(providerName)}/models/{modelId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateFallbackAsync(UpdateFallbackRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{httpClient.BaseAddress}api/admin/ai-providers/fallback", request, ct);
        response.EnsureSuccessStatusCode();
    }
}