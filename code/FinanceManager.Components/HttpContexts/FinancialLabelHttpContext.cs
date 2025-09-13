using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts.Entries;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpContexts;
public class FinancialLabelHttpContext(HttpClient httpClient)
{
    public Task<int> GetCount()
    {
        return httpClient.GetFromJsonAsync<int>($"{httpClient.BaseAddress}api/FinancialLabel/get-count");
    }
    public Task<FinancialLabel?> Get(int labelId)
    {
        return httpClient.GetFromJsonAsync<FinancialLabel>($"{httpClient.BaseAddress}api/FinancialLabel/get-by-id?id={labelId}");
    }
    public async Task<List<FinancialLabel>> Get(int index, int count)
    {
        var result = await httpClient.GetFromJsonAsync<List<FinancialLabel>>($"{httpClient.BaseAddress}api/FinancialLabel/get-by-index-and-count?index={index}&count={count}");

        if (result is null) return [];
        return result;
    }
    public async Task<bool> Add(AddFinancialLabel addFinancialLabel)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/FinancialLabel/add", addFinancialLabel);

            if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<bool>();
        }
        catch (Exception)
        {
            return false;
        }
        return false;
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
            var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/FinancialLabel/delete?id={id}");
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
