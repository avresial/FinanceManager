using FinanceManager.Application.Alerts.Models;
using FinanceManager.Domain.Alerts.Commands;
using FinanceManager.Domain.Alerts.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Alerts.HttpClients;

public sealed class FinancialAlertsHttpClient(HttpClient httpClient)
{
    private const string _endpoint = "api/FinancialAlerts";

    public async Task<List<FinancialAlertDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<FinancialAlertDto>>(_endpoint, cancellationToken);
        return result ?? [];
    }

    public async Task<FinancialAlertDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<FinancialAlertDto>($"{_endpoint}/{id}", cancellationToken);

    public async Task<FinancialAlertDto?> CreateAsync(
        CreateFinancialAlert command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(_endpoint, command, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<FinancialAlertDto>(cancellationToken: cancellationToken);
    }

    public async Task<FinancialAlertDto?> UpdateAsync(
        Guid id,
        UpdateFinancialAlert command,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{_endpoint}/{id}", command, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<FinancialAlertDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> SetEnabledAsync(
        Guid id,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PatchAsJsonAsync($"{_endpoint}/{id}/enabled", enabled, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{_endpoint}/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<AlertEvaluationOutcome>> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"{_endpoint}/evaluate", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<AlertEvaluationOutcome>>(cancellationToken: cancellationToken);
        return result ?? [];
    }
}