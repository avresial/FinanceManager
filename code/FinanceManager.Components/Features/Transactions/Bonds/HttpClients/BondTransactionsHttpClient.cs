using FinanceManager.Components.Features.Transactions.Shared.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Models;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Contracts.Transactions.Bonds;
using FinanceManager.Domain.FinancialAccounts.Bond.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Imports;
using System.Globalization;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Transactions.Bonds.HttpClients;

/// <summary>The caller's bond entries pooled per bond definition. No account is involved.</summary>
public class BondTransactionsHttpClient(HttpClient httpClient)
{
    private string Endpoint => $"{httpClient.BaseAddress}api/transactions/bonds";

    public async Task<BondTransactionLogItemDto> CreateAsync(SaveBondEntryRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Endpoint, request, cancellationToken);
        return await ReadEntry(response, cancellationToken);
    }

    public async Task<BondTransactionLogItemDto> UpdateAsync(long entryId, SaveBondEntryRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"{Endpoint}/{entryId}", request, cancellationToken);
        return await ReadEntry(response, cancellationToken);
    }

    public async Task DeleteAsync(long entryId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{Endpoint}/{entryId}", cancellationToken);
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
    }

    /// <summary>Imports rows for any of the caller's bond definitions. Overlapping rows come back as conflicts.</summary>
    public async Task<BondImportResult> ImportAsync(IReadOnlyList<BondEntryImportRecordDto> entries, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{Endpoint}/import", entries, cancellationToken);
        return await ReadImportResult(response, cancellationToken);
    }

    /// <summary>Confirms the accepted rows after review: the original rows of each accepted key, including the occurrences that already exist.</summary>
    public async Task<BondImportResult> ResolveImportAsync(IReadOnlyList<BondEntryImportRecordDto> acceptedEntries, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{Endpoint}/import/resolve", acceptedEntries, cancellationToken);
        return await ReadImportResult(response, cancellationToken);
    }

    public async Task<CsvDownload> ExportCsvAsync(int bondDetailsId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"{Endpoint}/export?bondDetailsId={bondDetailsId}&startDate={Uri.EscapeDataString(Utc(startDate))}&endDate={Uri.EscapeDataString(Utc(endDate))}", cancellationToken);
        return await OwnerTransactionsHttpResponse.ReadCsv(response, $"bond-{bondDetailsId}.csv", cancellationToken);
    }

    private static string Utc(DateTime date) => (date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime())
        .ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    private static async Task<BondTransactionLogItemDto> ReadEntry(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BondTransactionLogItemDto>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no entry.");
    }

    private static async Task<BondImportResult> ReadImportResult(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await OwnerTransactionsHttpResponse.EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BondImportResult>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned no import result.");
    }
}