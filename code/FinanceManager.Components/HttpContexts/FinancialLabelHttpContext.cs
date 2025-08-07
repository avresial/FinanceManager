using FinanceManager.Domain.Entities.Accounts.Entries;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpContexts;
public class FinancialLabelHttpContext(HttpClient httpClient)
{
    public async Task<int> GetCount()
    {
        return await httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/FinancialLabel/get-count");
    }
    public async Task<FinancialLabel?> Get(int labelId)
    {
        return await httpClient.GetFromJsonAsync<FinancialLabel>($"{httpClient.BaseAddress}api/FinancialLabel/get-by-id?label={labelId}");
    }
    public async Task<List<FinancialLabel>> Get(int index, int count)
    {
        var result = await httpClient.GetFromJsonAsync<List<FinancialLabel>>($"{httpClient.BaseAddress}api/FinancialLabel/get-by-index-and-count?index={index}&count={count}");

        if (result is null) return [];
        return result;
    }
    public async Task<bool> Add(string label)
    {
        try
        {
            var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/FinancialLabel/add?label={label}", null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public async Task<bool> UpdateName(int id, string name)
    {
        try
        {
            var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/FinancialLabel/update-name?id={id}&name={name}", null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public async Task<bool> Delete(int id)
    {
        try
        {
            var response = await httpClient.PostAsync($"{httpClient.BaseAddress}api/FinancialLabel/delete?id={id}", null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
