﻿using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts.Entries;
using System.Net.Http.Json;
using System.Threading;

namespace FinanceManager.Components.HttpContexts;
public class FinancialLabelHttpContext(HttpClient httpClient)
{
    public Task<int> GetCount(CancellationToken cancellationToken = default)
    {
        return httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/FinancialLabel/get-count", cancellationToken);
    }
    public Task<FinancialLabel?> Get(int labelId, CancellationToken cancellationToken = default)
    {
        return httpClient.GetFromJsonAsync<FinancialLabel>($"{httpClient.BaseAddress}api/FinancialLabel/get-by-id?id={labelId}", cancellationToken);
    }
    public async Task<List<FinancialLabel>> Get(int index, int count, CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<FinancialLabel>>($"{httpClient.BaseAddress}api/FinancialLabel/get-by-index-and-count?index={index}&count={count}", cancellationToken);

        return result ?? new List<FinancialLabel>();
    }
    public async Task<bool> Add(AddFinancialLabel addFinancialLabel, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/FinancialLabel/add", addFinancialLabel, cancellationToken);

            if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }
    public async Task<bool> UpdateName(int id, string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var encoded = Uri.EscapeDataString(name ?? string.Empty);
            var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/FinancialLabel/update-name?id={id}&name={encoded}", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/FinancialLabel/delete?id={id}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
