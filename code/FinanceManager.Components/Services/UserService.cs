using FinanceManager.Application.Commands.User;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserService> _logger;

    public UserService(HttpClient httpClient, ILogger<UserService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> AddUser(string login, string password, PricingLevel pricingLevel)
    {
        AddUser addUserCommand = new AddUser(login, password, pricingLevel);

        try
        {
            var response = await _httpClient.PostAsync($"{_httpClient.BaseAddress}api/User/Add",
                 JsonHelper.GenerateStringContent(JsonHelper.SerializeObj(addUserCommand)));
            var result = await response.Content.ReadFromJsonAsync<bool>();
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding user {login}", login);
        }
        return false;
    }

    public async Task<User?> GetUser(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<User>($"{_httpClient.BaseAddress}api/User/Get/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user {id}", id);
        }

        return null;
    }
}
