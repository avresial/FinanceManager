using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

public class DividendPaymentHttpClient(HttpClient httpClient)
{
    private static string Route(int accountId) => $"api/accounts/{accountId}/dividends";
    public async Task<DividendPaymentPage> GetHistoryPageAsync(int accountId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Route(accountId)}?page={page}&pageSize={pageSize}", cancellationToken);
        await RequireSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<DividendPaymentPage>(cancellationToken) ?? throw new HttpRequestException("The dividend history response was empty.");
    }
    public async Task<IReadOnlyList<DividendHoldingOption>> GetHoldingsAsync(int accountId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"{Route(accountId)}/holdings", cancellationToken);
        await RequireSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<DividendHoldingOption>>(cancellationToken) ?? throw new HttpRequestException("The dividend holding response was empty.");
    }
    public async Task<long> AddAsync(int accountId, DividendPaymentRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Route(accountId), request, cancellationToken);
        await RequireSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<long>(cancellationToken);
    }
    public async Task UpdateAsync(int accountId, long id, DividendPaymentRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Route(accountId)}/{id}", request, cancellationToken);
        await RequireSuccess(response, cancellationToken);
    }
    public async Task DeleteAsync(int accountId, long id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Route(accountId)}/{id}", cancellationToken);
        await RequireSuccess(response, cancellationToken);
    }
    private static async Task RequireSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.String) body = document.RootElement.GetString()!;
                else if (document.RootElement.TryGetProperty("detail", out var detail)) body = detail.GetString() ?? "Check the payment fields.";
                else body = "Check the payment fields.";
            }
            catch (JsonException) { }
            throw new HttpRequestException(body.Length <= 512 ? body : "Check the payment fields.");
        }
        throw new HttpRequestException(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
            ? "You do not have access to this investment account."
            : "The dividend request failed. Refresh and try again.");
    }
}