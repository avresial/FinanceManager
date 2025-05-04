using Blazored.LocalStorage;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Dtos;

namespace FinanceManager.Infrastructure.Repositories;

public class UserLocalStorageRepository : IUserRepository
{
    private readonly ILocalStorageService _localStorageService;

    public UserLocalStorageRepository(ILocalStorageService localStorageService)
    {
        _localStorageService = localStorageService;
    }

    async Task<User?> IUserRepository.GetUser(string login, string password)
    {
        login = login.ToLower();
        List<UserDto>? databaseUserDtos = await _localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return null;

        List<UserDto> userDtos = [];

        if (databaseUserDtos is not null)
            userDtos = databaseUserDtos;

        var foundUser = userDtos.FirstOrDefault(x => x.Login == login);
        if (foundUser is null) return null;
        if (foundUser.Password != password) return null;

        return new User()
        {
            Login = foundUser.Login,
            UserId = foundUser.Id,
            PricingLevel = foundUser.PricingLevel
        };
    }
    public async Task<User?> GetUser(int id)
    {
        List<UserDto>? databaseUserDtos = await _localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return null;

        List<UserDto> userDtos = [];

        if (databaseUserDtos is not null)
            userDtos = databaseUserDtos;

        var foundUser = userDtos.FirstOrDefault(x => x.Id == id);
        if (foundUser is null) return null;

        return new User()
        {
            Login = foundUser.Login,
            UserId = foundUser.Id,
            PricingLevel = foundUser.PricingLevel
        };
    }
    async Task<bool> IUserRepository.AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole)
    {
        login = login.ToLower();
        List<UserDto>? databaseUserDtos = await _localStorageService.GetItemAsync<List<UserDto>>("Users");
        List<UserDto> userDtos = [];

        if (databaseUserDtos is not null)
            userDtos = databaseUserDtos;

        if (userDtos.Any(x => x.Login == login))
            return false; // maybe throw exception?

        userDtos.Add(new UserDto()
        {
            Login = login,
            Password = password,
            Id = userDtos.Any() ? userDtos.Max(x => x.Id) + 1 : 0,
            PricingLevel = pricingLevel,
            UserRole = userRole
        });

        try
        {
            await _localStorageService.SetItemAsync("Users", userDtos);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }

        return true;
    }

    async Task<bool> IUserRepository.RemoveUser(int userId)
    {
        List<UserDto>? databaseUserDtos = await _localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return false;

        List<UserDto> userDtos = [];

        if (databaseUserDtos is not null)
            userDtos = databaseUserDtos;

        userDtos.RemoveAll(x => x.Id == userId);

        try
        {
            await _localStorageService.SetItemAsync("Users", userDtos);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }

        return true;
    }

    public Task<bool> UpdatePassword(int userId, string password)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel)
    {
        throw new NotImplementedException();
    }

    public Task<User?> GetUser(string login)
    {
        throw new NotImplementedException();
    }
}
