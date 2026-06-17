using FinanceManager.Api.Services;
using FinanceManager.Application.Commands.Login;
using FinanceManager.Domain.Identity.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using Xunit;

namespace FinanceManager.Tests.Integration.Controllers;

[Collection("api")]
public abstract class ControllerTests : IClassFixture<OptionsProvider>, IDisposable
{
    private readonly FinanceManagerApiTestApp _app;
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

    protected void ClearAuthorization()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    public ControllerTests(OptionsProvider optionsProvider, string environmentName = "test")
    {
        var authOptions = optionsProvider.Get<JwtAuthOptions>("JwtConfig");
        _jwtTokenGenerator = new JwtTokenGenerator(new OptionsWrapper<JwtAuthOptions>(authOptions));
        _app = new FinanceManagerApiTestApp(ConfigureServices, environmentName);
        Client = _app.Client;
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public virtual void Dispose()
    {
        ClearAuthorization();
        _app.Dispose();
    }
}