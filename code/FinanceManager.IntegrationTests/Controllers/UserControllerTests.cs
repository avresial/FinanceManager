using FinanceManager.Application.Commands.User;
using FinanceManager.Application.Services;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
public class UserControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider), IDisposable
{
    private const int _testUserId = 42;
    private const string _testUserName = "happyuser";
    private TestDatabase? _testDatabase;

    protected override void ConfigureServices(IServiceCollection services)
    {
        // Replace real DbContext with test in-memory context (similar to AdministrationUsersControllerTests)
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        _testDatabase = new TestDatabase();
        services.AddSingleton(_testDatabase.Context);

        // IUserPlanVerifier is still mocked for simplicity (only happy path capacity needed)
        var planVerifierMock = new Mock<IUserPlanVerifier>();
        planVerifierMock.Setup(x => x.GetUsedRecordsCapacity(_testUserId)).ReturnsAsync(5);
        services.AddSingleton(planVerifierMock.Object);
    }

    private async Task SeedUser()
    {
        if (_testDatabase is null) return;
        if (await _testDatabase.Context.Users.AnyAsync(x => x.Id == _testUserId, TestContext.Current.CancellationToken)) return;

        _testDatabase.Context.Users.Add(new UserDto
        {
            Id = _testUserId,
            Login = _testUserName,
            Password = "hash", // original password before update
            PricingLevel = PricingLevel.Basic,
            UserRole = UserRole.User,
            CreationDate = DateTime.UtcNow
        });
        await _testDatabase.Context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Add_ReturnsOkTrue()
    {
        // arrange (empty DB ensures login does not exist yet)
        AddUser cmd = new(_testUserName, "pw", PricingLevel.Basic);
        var userClient = new UserHttpClient(Client);

        // act
        var result = await userClient.AddUser(cmd);

        // assert
        Assert.True(result);
    }

    [Fact]
    public async Task Get_ReturnsUser()
    {
        // arrange
        await SeedUser();
        Authorize(_testUserName, _testUserId, UserRole.User);
        var userClient = new UserHttpClient(Client);

        // act
        var user = await userClient.GetUser(_testUserId);

        // assert
        Assert.NotNull(user);
        Assert.Equal(_testUserId, user!.UserId);
        Assert.Equal(_testUserName, user.Login);
    }

    [Fact]
    public async Task GetRecordCapacity_ReturnsCapacity()
    {
        // arrange
        await SeedUser();
        Authorize(_testUserName, _testUserId, UserRole.Admin);
        var userClient = new UserHttpClient(Client);

        // act
        var capacity = await userClient.GetRecordCapacity(_testUserId);

        // assert
        Assert.NotNull(capacity);
        Assert.True(capacity!.TotalCapacity > 0);
        Assert.Equal(5, capacity.UsedCapacity);
    }

    [Fact]
    public async Task UpdatePassword_ReturnsOkTrue_AndPasswordChanged()
    {
        // arrange
        await SeedUser();
        Authorize(_testUserName, _testUserId, UserRole.User);
        UpdatePassword cmd = new(_testUserId, "newpw");
        Assert.NotNull(_testDatabase);
        var originalPassword = (await _testDatabase!.Context.Users.FirstAsync(u => u.Id == _testUserId, TestContext.Current.CancellationToken)).Password;

        // act
        var result = await new UserHttpClient(Client).UpdatePassword(cmd);

        // assert http success abstraction
        Assert.True(result);

        // verify password changed in persistence layer
        var updatedPassword = (await _testDatabase.Context.Users.FirstAsync(u => u.Id == _testUserId, TestContext.Current.CancellationToken)).Password;
        Assert.NotEqual(originalPassword, updatedPassword);
        Assert.False(string.IsNullOrWhiteSpace(updatedPassword));
    }

    [Fact]
    public async Task UpdatePricingPlan_ChangesPricingLevel()
    {
        // arrange
        await SeedUser();
        Authorize(_testUserName, _testUserId, UserRole.Admin);
        Assert.NotNull(_testDatabase);
        var userClient = new UserHttpClient(Client);
        var originalPricing = (await _testDatabase!.Context.Users
            .FirstAsync(u => u.Id == _testUserId, TestContext.Current.CancellationToken)).PricingLevel;
        Assert.Equal(PricingLevel.Basic, originalPricing); // seeded value

        UpdatePricingPlan cmd = new(_testUserId, PricingLevel.Premium);

        // act
        var result = await userClient.UpdatePricingPlan(cmd);

        // assert 
        Assert.True(result);

        var apiUser = await userClient.GetUser(_testUserId);
        Assert.NotNull(apiUser);
        Assert.Equal(PricingLevel.Premium, apiUser!.PricingLevel);
    }

    [Fact]
    public async Task Delete_ReturnsOkTrue()
    {
        // arrange
        await SeedUser();
        Authorize(_testUserName, _testUserId, UserRole.Admin);

        // act
        var result = await new UserHttpClient(Client).Delete(_testUserId);

        // assert
        Assert.True(result);
    }

    public override void Dispose()
    {
        base.Dispose();
        _testDatabase?.Dispose();
        _testDatabase = null;
        GC.SuppressFinalize(this);
    }
}