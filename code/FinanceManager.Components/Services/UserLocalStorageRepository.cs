using Blazored.LocalStorage;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Repositories;

public class UserLocalStorageRepository(ILocalStorageService localStorageService, ILogger<UserLocalStorageRepository> logger) : IUserRepository
{
    async Task<User?> IUserRepository.GetUser(string login, string password)
    {
        login = login.ToLower();
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return null;

        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        var foundUser = userDtos.FirstOrDefault(x => x.Login == login);
        if (foundUser is null) return null;
        if (foundUser.Password != password) return null;

        return new()
        {
            Login = foundUser.Login,
            UserId = foundUser.Id,
            PricingLevel = foundUser.PricingLevel,
            CreationDate = foundUser.CreationDate,
        };
    }
    public async Task<User?> GetUser(int id)
    {
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return null;

        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        var foundUser = userDtos.FirstOrDefault(x => x.Id == id);
        if (foundUser is null) return null;

        return new()
        {
            Login = foundUser.Login,
            UserId = foundUser.Id,
            PricingLevel = foundUser.PricingLevel,
            CreationDate = foundUser.CreationDate,
        };
    }
    async Task<bool> IUserRepository.AddUser(string login, string password, PricingLevel pricingLevel, UserRole userRole, string? firstName, string? lastName)
    {
        login = login.ToLower();
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        if (userDtos.Any(x => x.Login == login))
            return false; // maybe throw exception?

        userDtos.Add(new()
        {
            Login = login,
            FirstName = firstName,
            LastName = lastName,
            Password = password,
            Id = userDtos.Any() ? userDtos.Max(x => x.Id) + 1 : 0,
            PricingLevel = pricingLevel,
            UserRole = userRole,
            CreationDate = DateTime.UtcNow,
        });

        try
        {
            await localStorageService.SetItemAsync("Users", userDtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding user to local storage.");
            return false;
        }

        return true;
    }

    Task<bool> IUserRepository.AddUserWithId(int userId, string login, string password, PricingLevel pricingLevel, UserRole userRole, string? firstName, string? lastName)
        => throw new NotImplementedException();

    async Task<bool> IUserRepository.RemoveUser(int userId)
    {
        var databaseUserDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users");
        if (databaseUserDtos is null) return false;

        var userDtos = databaseUserDtos is not null ? databaseUserDtos : [];

        userDtos.RemoveAll(x => x.Id == userId);

        try
        {
            await localStorageService.SetItemAsync("Users", userDtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing user from local storage.");
            return false;
        }

        return true;
    }

    public async Task<LoginThrottlingState?> GetLoginThrottlingState(string login)
    {
        var foundUser = await FindUser(login);
        if (foundUser is null) return null;

        return new LoginThrottlingState(foundUser.FailedLoginAttempts, foundUser.LockoutEndUtc);
    }

    public async Task<int?> IncrementFailedLoginAttempts(string login)
    {
        var (userDtos, foundUser) = await LoadUser(login);
        if (foundUser is null) return null;

        foundUser.FailedLoginAttempts++;
        await Persist(userDtos);
        return foundUser.FailedLoginAttempts;
    }

    public async Task<bool> LockAccount(string login, DateTime lockoutEndUtc)
    {
        var (userDtos, foundUser) = await LoadUser(login);
        if (foundUser is null) return false;

        foundUser.LockoutEndUtc = lockoutEndUtc;
        foundUser.FailedLoginAttempts = 0;
        await Persist(userDtos);
        return true;
    }

    public async Task<bool> ResetLoginThrottling(string login)
    {
        var (userDtos, foundUser) = await LoadUser(login);
        if (foundUser is null) return false;

        foundUser.LockoutEndUtc = null;
        foundUser.FailedLoginAttempts = 0;
        await Persist(userDtos);
        return true;
    }

    private async Task<UserDto?> FindUser(string login)
    {
        var (_, foundUser) = await LoadUser(login);
        return foundUser;
    }

    private async Task<(List<UserDto> Users, UserDto? Found)> LoadUser(string login)
    {
        login = login.ToLower();
        var userDtos = await localStorageService.GetItemAsync<List<UserDto>>("Users") ?? [];
        return (userDtos, userDtos.FirstOrDefault(x => x.Login == login));
    }

    private Task Persist(List<UserDto> userDtos) => localStorageService.SetItemAsync("Users", userDtos).AsTask();

    public Task<bool> UpdatePassword(int userId, string password) => throw new NotImplementedException();
    public Task<bool> UpdatePricingPlan(int userId, PricingLevel pricingLevel) => throw new NotImplementedException();
    public Task<User?> GetUser(string login) => throw new NotImplementedException();
    public Task<int> GetUsersCount() => throw new NotImplementedException();
    public IAsyncEnumerable<User> GetUsers(DateTime startDate, DateTime endDate) => throw new NotImplementedException();
    public IAsyncEnumerable<User> GetUsers(int recordIndex, int recordsCount) => throw new NotImplementedException();
    public IAsyncEnumerable<int> GetUsersIds(int recordIndex, int recordsCount) => throw new NotImplementedException();
}