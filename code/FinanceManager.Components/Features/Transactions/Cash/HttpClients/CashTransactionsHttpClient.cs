using FinanceManager.Components.Features.Transactions.Shared.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Models;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Cash;
using FinanceManager.Domain.Transactions.Cash.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Transactions.Cash.HttpClients;

/// <summary>The caller's cash entries pooled per currency. No account is involved.</summary>
public class CashTransactionsHttpClient(HttpClient httpClient)
{
    private string Endpoint => $"{httpClient.BaseAddress}api/transactions/cash";

    private const int _maxEntryIdsPerRequest = 100;

    /// <summary>
    /// The caller's entries with exactly these ids, newest first. Ids are de-duplicated, an empty list makes no request,
    /// and long lists are split into requests of at most 100 ids. Entries that are missing or not the caller's are skipped.
    /// </summary>
    public async Task<IReadOnlyList<CurrencyTransactionLogItemDto>> GetByIdsAsync(IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default)
    {
        var ids = entryIds.Distinct().ToArray();
        List<CurrencyTransactionLogItemDto> entries = [];
        foreach (var chunk in ids.Chunk(_maxEntryIdsPerRequest))
        {
            var query = string.Join('&', chunk.Select(id => $"entryIds={id}"));
            using var response = await httpClient.GetAsync($"{Endpoint}?{query}", cancellationToken);
            await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
            entries.AddRange(await response.Content.ReadFromJsonAsync<List<CurrencyTransactionLogItemDto>>(cancellationToken)
                ?? throw new InvalidOperationException("The server returned no entries."));
        }

        return [.. entries.OrderByDescending(entry => entry.PostingDate).ThenByDescending(entry => entry.EntryId)];
    }

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

    public async Task<CurrencyImportJobStatusDto?> GetCurrencyImportStatusAsync(Guid jobId)
    {
        using var response = await httpClient.GetAsync($"{Endpoint}/import/jobs/{jobId}");
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<CurrencyImportJobStatusDto?>();
    }

    public async Task<CurrencyImportJobStatusDto?> ResolveAsyncCurrencyImportConflictsAsync(CurrencyImportResolveConflictsRequestDto request)
    {
        using var response = await httpClient.PostAsJsonAsync($"{Endpoint}/import/jobs/{request.JobId}/resolve", request);
        if (!response.IsSuccessStatusCode) throw new Exception(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<CurrencyImportJobStatusDto?>();
    }
}