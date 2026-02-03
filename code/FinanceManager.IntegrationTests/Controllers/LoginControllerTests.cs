using FinanceManager.Application.Commands.Login;
using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Users;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net.Http.Json;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;

[Collection("api")]
[Trait("Category", "Integration")]
public class LoginControllerTests(OptionsProvider optionsProvider) : ControllerTests(optionsProvider)
{
    private static readonly string _testUserName = "testUser";
    private static readonly string _testPassword = "password";

    protected override void ConfigureServices(IServiceCollection services)
    {
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(x => x.GetUser(_testUserName, PasswordEncryptionProvider.EncryptPassword(PasswordEncryptionProvider.EncryptPassword(_testPassword))))
            .ReturnsAsync(new User
            {
                UserId = 99,
                Login = _testUserName,
                UserRole = UserRole.User,
                CreationDate = DateTime.UtcNow
            });

        services.AddSingleton(userRepoMock.Object);

        Mock<IActiveUsersRepository> activeUsersMock = new();
        activeUsersMock.Setup(x => x.Add(It.IsAny<int>(), It.IsAny<DateOnly>())).Returns(Task.CompletedTask);
        services.AddSingleton(activeUsersMock.Object);
    }

    [Fact]
    public async Task Login_ReturnsToken_ForValidCredentials()
    {
        // arrange
        LoginRequestModel request = new(_testUserName, PasswordEncryptionProvider.EncryptPassword(_testPassword));

        // act
        var response = await Client.PostAsJsonAsync("api/Login", request, cancellationToken: TestContext.Current.CancellationToken);

        // assert
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponseModel>(cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal(_testUserName, body.UserName);
        Assert.Equal(99, body.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }
}