using FinanceManager.Application.TransactionRules.Services;
using FinanceManager.Domain.TransactionRules;
using FinanceManager.Domain.TransactionRules.Commands;
using FinanceManager.Domain.TransactionRules.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.TransactionRules.HttpClients;

public sealed class TransactionRuleHttpClient(HttpClient httpClient)
{
    private const string _endpoint = "api/TransactionRules";

    public async Task<List<TransactionRuleDto>> GetAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<TransactionRuleDto>>(_endpoint, cancellationToken);
        return result ?? [];
    }

    public async Task<TransactionRuleDto?> CreateAsync(CreateTransactionRule command, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(_endpoint, command, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TransactionRuleDto>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<TransactionRuleDto?> UpdateAsync(Guid id, UpdateTransactionRule command, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{_endpoint}/{id}", command, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TransactionRuleDto>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<bool> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PatchAsJsonAsync($"{_endpoint}/{id}/enabled", enabled, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{_endpoint}/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<List<TransactionRuleDto>?> ReorderAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{_endpoint}/reorder", new ReorderTransactionRules(ids), cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<List<TransactionRuleDto>>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<TransactionRuleEngineResult?> PreviewAsync(TransactionRulePreviewFacts facts, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{_endpoint}/preview", facts, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TransactionRuleEngineResult>(cancellationToken: cancellationToken)
            : null;
    }

    public async Task<TransactionRuleApplyResultDto?> ApplyAsync(ApplyTransactionRules command, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{_endpoint}/apply", command, cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<TransactionRuleApplyResultDto>(cancellationToken: cancellationToken)
            : null;
    }
}