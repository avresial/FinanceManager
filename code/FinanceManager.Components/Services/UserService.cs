using FinanceManager.Application.Commands.User;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Services;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;

    public UserService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> AddUser(string login, string password)
    {
        AddUser addUserCommand = new AddUser(login, password);
        HttpResponseMessage? response = null;

        try
        {
            response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/User/Add",
                JsonHelper.GenerateStringContent(JsonHelper.SerializeObj(addUserCommand)));
            var result = await response.Content.ReadFromJsonAsync<bool>();
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        return false;
    }
}
