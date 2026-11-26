using FinanceManager.Components.Features.Transactions.Shared.HttpClients;
using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Contracts.Transactions.Investments;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Transactions.Investments.HttpClients;

/// <summary>The caller's trades pooled across brokers. No account is involved.</summary>
public class InvestmentTradesHttpClient(HttpClient httpClient)
{
    private string Endpoint => $"{httpClient.BaseAddress}api/transactions/investment";

    public async Task<IReadOnlyList<InvestmentHoldingDto>> GetHoldingsAsync(DateOnly asOf, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<InvestmentHoldingDto>>($"{Endpoint}/holdings?asOf={asOf:yyyy-MM-dd}", cancellationToken)
            ?? throw new InvalidOperationException("The server returned no holdings response.");

    public async Task<InvestmentTransactionDto> CreateAsync(CreateInvestmentTradeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Endpoint, request, cancellationToken);
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no trade.");
    }

    public async Task UpdateAsync(long id, UpdateInvestmentTradeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Endpoint}/{id}", request, cancellationToken);
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Endpoint}/{id}", cancellationToken);
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
    }
}