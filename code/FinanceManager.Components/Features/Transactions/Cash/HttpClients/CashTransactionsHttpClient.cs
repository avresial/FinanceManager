using FinanceManager.Components.Features.Transactions.Shared.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Models;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.FinancialAccounts.Currencies.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Transactions.Cash.HttpClients;

/// <summary>The caller's cash entries pooled per currency. No account is involved.</summary>
public class CashTransactionsHttpClient(HttpClient httpClient)
{
    private string Endpoint => $"{httpClient.BaseAddress}api/transactions/cash";

    public async Task<CurrencyTransactionLogItemDto> CreateAsync(CreateCashEntryRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Endpoint, request, cancellationToken);
        return await ReadEntry(response, cancellationToken);
    }

    public async Task<CurrencyTransactionLogItemDto> UpdateAsync(long entryId, UpdateCashEntryRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Endpoint}/{entryId}", request, cancellationToken);
        return await ReadEntry(response, cancellationToken);
    }

    public async Task DeleteAsync(long entryId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Endpoint}/{entryId}", cancellationToken);
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
    }

    /// <summary>Starts an import into the caller's pool of one currency. Follow the job through the existing import status and SignalR hub.</summary>
    public async Task<CurrencyImportJobStartResponseDto> StartImportAsync(
        int currencyId, IReadOnlyList<CurrencyEntryImportRecordDto> entries, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{Endpoint}/import?currencyId={currencyId}", entries, cancellationToken);
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CurrencyImportJobStartResponseDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no import job.");
    }

    public async Task<CsvDownload> ExportCsvAsync(int currencyId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"{Endpoint}/export?currencyId={currencyId}&startDate={Uri.EscapeDataString(OwnerTransactionsHttpResponse.Utc(startDate))}&endDate={Uri.EscapeDataString(OwnerTransactionsHttpResponse.Utc(endDate))}", cancellationToken);
        return await OwnerTransactionsHttpResponse.ReadCsv(response, $"cash-{currencyId}.csv", cancellationToken);
    }

    private static async Task<CurrencyTransactionLogItemDto> ReadEntry(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<CurrencyTransactionLogItemDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no entry.");
    }
}