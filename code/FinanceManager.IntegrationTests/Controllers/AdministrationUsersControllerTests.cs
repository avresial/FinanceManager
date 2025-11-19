using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class AdministrationUsersControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private TestDatabase? _testDatabase;
    private readonly DateTime _nowUtc = DateTime.UtcNow;

    protected override void ConfigureServices(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase!.Context);
    }

    private async Task SeedTestUsers(int userCount = 5)
    {
        for (int i = 1; i <= userCount; i++)
        {
            if (await _testDatabase!.Context.Users.AnyAsync(x => x.Id == i))
                continue;

            _testDatabase.Context.Users.Add(new UserDto
            {
                Id = i,
                Login = $"testuser{i}",
                Password = "hash",
                PricingLevel = PricingLevel.Free,
                UserRole = i == 1 ? UserRole.Admin : UserRole.User,
                CreationDate = _nowUtc.AddDays(-userCount + i)
            });
        }

        await _testDatabase!.Context.SaveChangesAsync();
    }

    private async Task SeedTestAccounts(int accountCount = 3)
    {
        await SeedTestUsers();

        for (int i = 1; i <= accountCount; i++)
        {
            if (await _testDatabase!.Context.Accounts.AnyAsync(x => x.AccountId == i))
                continue;

            _testDatabase.Context.Accounts.Add(new FinancialAccountBaseDto
            {
                AccountId = i,
                UserId = 1,
                Name = $"Test Account {i}",
                AccountLabel = AccountLabel.Cash,
                AccountType = AccountType.Bank
            });
        }

        await _testDatabase!.Context.SaveChangesAsync();
    }

    private async Task SeedTestActiveUsers()
    {
        await SeedTestUsers();

        for (int i = 0; i < 10; i++)
        {
            _testDatabase!.Context.ActiveUsers.Add(new ActiveUser
            {
                UserId = 1,
                LoginTime = _nowUtc.AddDays(-i).Date
            });
        }

        await _testDatabase!.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetNewUsersDaily_WithoutAuth_ReturnsEmptyList()
    {
        // Arrange - No authorization
        await SeedTestUsers(10);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetNewUsersDaily();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNewUsersDaily_WithAuth_ReturnsData()
    {
        // Arrange
        await SeedTestUsers(10);
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetNewUsersDaily();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetNewUsersDaily_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetNewUsersDaily();

        // Assert
        Assert.NotNull(result);
        Assert.All(result, item => Assert.Equal(0m, item.Value));
    }

    [Fact]
    public async Task GetDailyActiveUsers_WithAuth_ReturnsData()
    {
        // Arrange
        await SeedTestActiveUsers();
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetDailyActiveUsers();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetAccountsCount_WithAuth_ReturnsCorrectCount()
    {
        // Arrange
        await SeedTestAccounts(5);
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetAccountsCount();

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public async Task GetTotalTrackedMoney_WithData_ReturnsValue()
    {
        // Arrange
        await SeedTestAccounts();
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetTotalTrackedMoney();

        // Assert
        Assert.True(result == null || result >= 0);
    }

    [Fact]
    public async Task GetUsersCount_WithAuth_ReturnsCorrectCount()
    {
        // Arrange
        await SeedTestUsers(7);
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetUsersCount();

        // Assert
        Assert.Equal(7, result);
    }

    [Fact]
    public async Task GetUsers_WithValidPagination_ReturnsData()
    {
        // Arrange
        await SeedTestUsers(15);
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetUsers(0, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task GetUsers_WithSecondPage_ReturnsRemainingData()
    {
        // Arrange
        await SeedTestUsers(15);
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetUsers(10, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task GetUsers_WithInvalidRecordIndex_ReturnsEmptyList()
    {
        // Arrange
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetUsers(-1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUsers_WithInvalidRecordsCount_ReturnsEmptyList()
    {
        // Arrange
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetUsers(0, 0);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUsers_WithNegativeRecordsCount_ReturnsEmptyList()
    {
        // Arrange
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetUsers(0, -5);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUsers_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        Authorize("AdminUser", 1, UserRole.Admin);
        var context = new AdministrationUsersHttpClient(Client);

        // Act
        var result = await context.GetUsers(0, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    public void Dispose()
    {
        if (_testDatabase is null)
            return;

        _testDatabase.Dispose();
        _testDatabase = null;
    }
}
