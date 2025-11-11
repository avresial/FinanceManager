using FinanceManager.Api.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Xunit;

namespace FinanceManager.IntegrationTests.Controllers;
[Collection("api")]
public abstract class ControllerTests : IClassFixture<OptionsProvider>
{
    private readonly JwtTokenGenerator? _jwtTokenGenerator;
    protected HttpClient Client { get; }

    protected LoginResponseModel? Authorize(string userName, int userId, UserRole role)
    {
        if (_jwtTokenGenerator is null) return null;
        var jwt = _jwtTokenGenerator.GenerateToken(userName, userId, role);

        if (jwt is null) return null;

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt.AccessToken);

        return jwt;
    }

    public ControllerTests(OptionsProvider optionsProvider)
    {
        var authOptions = optionsProvider.Get<JwtAuthOptions>("JwtConfig");
        _jwtTokenGenerator = new JwtTokenGenerator(new OptionsWrapper<JwtAuthOptions>(authOptions));
        var app = new FinanceManagerApiTestApp(ConfigureServices);
        Client = app.Client;
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }
}