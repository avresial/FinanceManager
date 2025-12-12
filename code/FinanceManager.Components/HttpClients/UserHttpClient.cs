using FinanceManager.Application.Commands.User;
using FinanceManager.Domain.Entities.Users;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class UserHttpClient(HttpClient httpClient)
{
    public async Task<bool> AddUser(AddUser addUser)
    {
        var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/User/Add", addUser);
        return response.IsSuccessStatusCode;
    }

    public Task<User?> GetUser(int id) => httpClient.GetFromJsonAsync<User>($"{httpClient.BaseAddress}api/User/Get/{id}");

    public Task<RecordCapacity?> GetRecordCapacity(int userId) => httpClient.GetFromJsonAsync<RecordCapacity>($"{httpClient.BaseAddress}api/User/GetRecordCapacity/{userId}");

    public async Task<bool> Delete(int userId)
    {
        var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/User/Delete/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdatePassword(UpdatePassword updatePassword)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/User/UpdatePassword/", updatePassword);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdatePricingPlan(UpdatePricingPlan updatePricingPlan)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/User/UpdatePricingPlan/", updatePricingPlan);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateRole(UpdateUserRole updateUserRole)
    {
        var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/User/UpdateUserRole/", updateUserRole);
        return response.IsSuccessStatusCode;
    }
}